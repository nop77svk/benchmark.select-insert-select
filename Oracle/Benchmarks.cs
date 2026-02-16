namespace DatabaseMultiLockBenchmark.Oracle;
#pragma warning disable SA1116

using System;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using Dapper;

using global::Oracle.ManagedDataAccess.Client;

[InProcess]
public class Benchmarks
    : IDisposable
{
    private static readonly object _benchmarkBindVars = new { i_a = 1, i_b = 5 };
    private readonly OracleConnection _persistentUserConnection;

    private bool _disposedValue;

    public Benchmarks()
    {
        _persistentUserConnection = new OracleConnection(StaticGlobalContext.UserConnectionString);
    }

    [GlobalSetup]
    public void BenchmarkSetUp()
    {
        Console.Out.WriteLine($"*** Connecting to: {_persistentUserConnection.ConnectionString}");
        _persistentUserConnection.Open();
        TruncateBenchmarkTables();
    }

    [GlobalCleanup]
    public void BenchmarkTearDown()
    {
        TruncateBenchmarkTables();
        _persistentUserConnection.Close();
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
            exception
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
                    exception
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
                    lockname => 'a:'||i_a||'|b:'||i_b,
                    lockhandle => l_lock_handle
                );

                -- https://docs.oracle.com/en/database/oracle/oracle-database/19/arpls/DBMS_LOCK.html#GUID-CC3AEC00-CBFF-45DD-99C3-C7A312C0213E
                l_lock_request_result := dbms_lock.request(
                    lockhandle => l_lock_handle,
                    release_on_commit => true
                );

                if l_lock_request_result not in (0, 4) then
                    raise_application_error(-20000, 'Failed to acquire lock on record (a = '||i_a||', b = '||i_b||') with result of '||l_lock_request_result);
                end if;

                begin
                    select id
                    into o_id
                    from t_data
                    where a = i_a and b = i_b;
                exception
                    when no_data_found then
                        insert into t_data (a, b)
                        values (i_a, i_b)
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
                insert into t_data_lock (a, b)
                values (i_a, i_b);

                begin
                    select id
                    into o_id
                    from t_data
                    where a = i_a and b = i_b;
                exception
                    when no_data_found then
                        insert into t_data (a, b)
                        values (i_a, i_b)
                        returning id into o_id;
                end;

                delete from t_data_lock
                where a = i_a and b = i_b;
            end;
            """,
            _benchmarkBindVars
        );
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _persistentUserConnection.Close();
                _persistentUserConnection.Dispose();
            }

            _disposedValue = true;
        }
    }

    private void TruncateBenchmarkTables()
    {
        _persistentUserConnection.Execute("truncate table t_data_lock drop storage;");
        _persistentUserConnection.Execute("truncate table t_data drop storage;");
    }
}
