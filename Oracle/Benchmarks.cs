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
    private const int OracleContainerHostPort = 1532;
    private static readonly string OracleContainerPassword = Guid.NewGuid().ToString();

    private static readonly Uri _dockerDaemonUri = new UriBuilder("http", "localhost", 2375).Uri;

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
        TruncateBenchmarkTables();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        TruncateBenchmarkTables();
    }

    [Benchmark]
    public async ValueTask EmptyBenchmark()
    {
        await ValueTask.CompletedTask;
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
