#pragma warning disable SA1116
namespace DatabaseMultiLockBenchmark.Oracle;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using Dapper;

using global::Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

public class Benchmarks
    : IAsyncDisposable
{
    private const int OracleContainerHostPort = 1522;
    private static readonly string OracleContainerPassword = Guid.NewGuid().ToString();

    private static readonly Uri _dockerDaemonUri = new UriBuilder("http", "localhost", 2375).Uri;
    private static readonly object _benchmarkBindVars = new { i_a = 1, i_b = 5 };

    private readonly OracleContainer _databaseContainer = new OracleBuilder("container-registry.oracle.com/database/free:latest-lite")
        .WithDockerEndpoint(_dockerDaemonUri)
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .WithPortBinding(OracleContainerHostPort, 1521)
        .WithEnvironment(new Dictionary<string, string>()
        {
            ["ORACLE_PWD"] = OracleContainerPassword,
            ["ORACLE_CHARACTERSET"] = "al32utf8",
            ["ENABLE_ARCHIVELOG"] = "false",
            ["ENABLE_FORCE_LOGGING"] = "false"
        })
        .Build();

    private readonly OracleConnectionStringBuilder _dbaConnectionBuilder = new OracleConnectionStringBuilder()
    {
        DataSource = $"127.0.0.1:{OracleContainerHostPort}/FREEPDB1",
        UserID = "SYS",
        Password = OracleContainerPassword,
        DBAPrivilege = "SYSDBA"
    };

    private readonly OracleConnectionStringBuilder _userConnectionBuilder = new OracleConnectionStringBuilder()
    {
        DataSource = $"127.0.0.1:{OracleContainerHostPort}/FREEPDB1",
        UserID = "BENCHMARK_OWNER",
        Password = OracleContainerPassword
    };

    private bool _disposedValue;

    private OracleConnection? _persistentUserConnection = null;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        await Console.Out.WriteLineAsync("Starting database server instance");
        await _databaseContainer.StartAsync();

        await Console.Out.WriteLineAsync("Setting up benchmark DB schema");
        await using (var dbaConnection = new OracleConnection(_dbaConnectionBuilder.ConnectionString))
        {
            await dbaConnection.OpenAsync();

            await Console.Out.WriteLineAsync(" * Create tablespace");
            await dbaConnection.ExecuteAsync("""
                create smallfile tablespace benchmark_tbs
                datafile '/opt/oracle/oradata/FREE/FREEPDB1/benchmark_tbs_01.dbf' size 16m
                autoextend on next 16m maxsize unlimited
                segment space management auto
                extent management local autoallocate;
            """);

            await Console.Out.WriteLineAsync(" * Create test user");
            await dbaConnection.ExecuteAsync($"""
                create user "{_userConnectionBuilder.UserID}"
                    identified by "{_userConnectionBuilder.Password}"
                default tablespace benchmark_tbs
                temporary tablespace temp
                account unlock;
            """);

            await dbaConnection.ExecuteAsync($"""
                grant create session, create table, create sequence, create procedure
                to {_userConnectionBuilder.UserID};
            """);

            await dbaConnection.ExecuteAsync($"""
                grant execute on sys.dbms_lock
                to {_userConnectionBuilder.UserID};
            """);
        }

        await using (var userConnection = new OracleConnection(_userConnectionBuilder.ConnectionString))
        {
            await userConnection.OpenAsync();

            await Console.Out.WriteLineAsync(" * Create data table");
            await userConnection.ExecuteAsync($"""
                create table t_data
                (
                    id              integer generated always as identity not null,
                    constraint PK_data primary key (id),
                    a               integer not null,
                    b               integer not null,
                    constraint PK_data_2 unique (a, b) using index
                );
            """);

            await Console.Out.WriteLineAsync(" * Create locks table");
            await userConnection.ExecuteAsync($"""
                create table t_data_lock
                (
                    a               integer not null,
                    b               integer not null,
                    constraint PK_data_lock primary key (a, b)
                )
                organization index;
            """);

            await userConnection.CloseAsync();
        }

        await Console.Out.WriteLineAsync("DB instance prepared!");
    }

    [GlobalCleanup]
    public async Task GlobalTearDown()
    {
        await _databaseContainer.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        await DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _persistentUserConnection = new OracleConnection(_userConnectionBuilder.ConnectionString);
        _persistentUserConnection.Open();
        TruncateBenchmarkTables();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        TruncateBenchmarkTables();
        _persistentUserConnection?.Close();
        _persistentUserConnection?.Dispose();
    }

    [Benchmark]
    public async ValueTask Solution_1_Insert_Select()
    {
        ArgumentNullException.ThrowIfNull(_persistentUserConnection);

        await _persistentUserConnection.ExecuteAsync("""
            declare
                i_a             t_data.a%type := :i_a;
                i_b             t_data.b%type := :i_b;
                o_id            t_data.id%type;
            begin
                insert into t_data (a, b)
                values (i_a, i_b)
                returning id into o_id;
            excption
                when dup_val_on_index then
                    select id
                    into o_id
                    from t_data
                    where a = i_a and b = i_b;
            end;
            """,
            _benchmarkBindVars
        );
    }

    [Benchmark]
    public async ValueTask Solution_2_Select_Insert_Select()
    {
        ArgumentNullException.ThrowIfNull(_persistentUserConnection);

        await _persistentUserConnection.ExecuteAsync("""
            declare
                i_a             t_data.a%type := :i_a;
                i_b             t_data.b%type := :i_b;
                o_id            t_data.id%type;
            begin
                select id
                into o_id
                from t_data
                where a = i_a and b = i_b;
            exception
                when no_data_found then
                    begin
                        insert into t_data (a, b)
                        values (i_a, i_b)
                        returning id into o_id;
                    excption
                        when dup_val_on_index then
                            select id
                            into o_id
                            from t_data
                            where a = i_a and b = i_b;
                    end;
            end;
            """,
            _benchmarkBindVars
        );
    }

    [Benchmark]
    public async ValueTask Solution_3_LockViaDbmsLock()
    {
        ArgumentNullException.ThrowIfNull(_persistentUserConnection);

        await _persistentUserConnection.ExecuteAsync("""
            declare
                i_a             t_data.a%type := :i_a;
                i_b             t_data.b%type := :i_b;
                o_id            t_data.id%type;

                l_lock_handle           varchar2(128);
                l_lock_request_result   integer;
            begin
                dbms_lock.allocate_unique_autonomous(
                    lockname => 'a:'||l_a||'|b:'||l_b,
                    lockhandle => l_lock_handle
                );

                -- https://docs.oracle.com/en/database/oracle/oracle-database/19/arpls/DBMS_LOCK.html#GUID-CC3AEC00-CBFF-45DD-99C3-C7A312C0213E
                l_lock_request_result := dbms_lock.request(
                    lockhandle => l_lock_handle,
                    release_on_commit => true
                );

                if l_lock_request_result not in (0, 4) then
                    raise_application_error(-20000, 'Failed to acquire lock on record (a = '||l_a||', b = '||l_b||') with result of '||l_lock_request_result);
                end if;

                begin
                    select id
                    into o_id
                    from t_data
                    where a = l_a and b = l_b;
                exception
                    when no_data_found then
                        insert into t_data (a, b)
                        values (l_a, l_b)
                        returning id into o_id;
                end;
            end;
            """,
            _benchmarkBindVars
        );
    }

    [Benchmark]
    public async ValueTask Solution_4_LockViaLocksTable()
    {
        ArgumentNullException.ThrowIfNull(_persistentUserConnection);

        await _persistentUserConnection.ExecuteAsync("""
            declare
                i_a             t_data.a%type := :i_a;
                i_b             t_data.b%type := :i_b;
                o_id            t_data.id%type;

                l_lock_handle           varchar2(128);
                l_lock_request_result   integer;
            begin
                insert into t_data_locks (a, b)
                values (i_a, i_b);

                begin
                    select id
                    into o_id
                    from t_data
                    where a = l_a and b = l_b;
                exception
                    when no_data_found then
                        insert into t_data (a, b)
                        values (l_a, l_b)
                        returning id into o_id;
                end;

                delete from t_data_locks
                where a = i_a and b = i_b;
            end;
            """,
            _benchmarkBindVars
        );
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                await _databaseContainer.DisposeAsync();
            }

            _disposedValue = true;
        }
    }

    private void TruncateBenchmarkTables()
    {
        using var userConnection = new OracleConnection(_userConnectionBuilder.ConnectionString);
        userConnection.Open();
        userConnection.Execute("truncate table t_data_lock drop storage;");
        userConnection.Execute("truncate table t_data drop storage;");
        userConnection.Close();
    }
}
