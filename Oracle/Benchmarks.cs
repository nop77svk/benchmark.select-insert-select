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
    private const string SqlSolution1InsertSelect = """
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
        """;

    private const string SqlSolution2SelectInsert = """
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
        """;

    private const string SqlSolution3AppLockSelectInsert = """
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
        """;

    private const string SqlSolution4DbLockSelectInsertSelect = """
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
        """;

    private static readonly object _benchmarkBindVars = new { i_a = 1, i_b = 5 };
    private readonly OracleConnection _persistentUserConnection;

    private bool _disposedValue;

    public Benchmarks()
    {
        using (ContainerFactory containerFactory = new ContainerFactory())
        {
            Console.Out.WriteLine($"Connecting to: {containerFactory.UserConnectionString}");
            StaticGlobalContext.UserConnectionString = containerFactory.UserConnectionString;
        }

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

    [IterationSetup(Targets = [
        nameof(Solution1_InsertSelect_WhenDataExist),
        nameof(Solution1_InsertSelect_WhenDataExist_Async),
        nameof(Solution2_SelectInsertSelect_WhenDataExist),
        nameof(Solution2_SelectInsertSelect_WhenDataExist_Async),
        nameof(Solution3_AppLockSelectInsert_WhenDataExist),
        nameof(Solution3_AppLockSelectInsert_WhenDataExist_Async),
        nameof(Solution4_DbLockSelectInsert_WhenDataExist),
        nameof(Solution4_DbLockSelectInsert_WhenDataExist_Async)
    ])]
    public void IterationSetup()
    {
        _persistentUserConnection.Execute(@"insert into t_data (a, b) values (:i_a, :i_b);", _benchmarkBindVars);
    }

    [Benchmark]
    public async ValueTask Solution1_InsertSelect_ToEmpty_Async()
        => await ExecuteTestCaseInTransactionAsync(SqlSolution1InsertSelect, false);

    [Benchmark]
    public async ValueTask Solution1_InsertSelect_WhenDataExist_Async()
        => await ExecuteTestCaseInTransactionAsync(SqlSolution1InsertSelect, false);

    [Benchmark]
    public async ValueTask Solution2_SelectInsertSelect_ToEmpty_Async()
        => await ExecuteTestCaseInTransactionAsync(SqlSolution2SelectInsert, false);

    [Benchmark]
    public async ValueTask Solution2_SelectInsertSelect_WhenDataExist_Async()
        => await ExecuteTestCaseInTransactionAsync(SqlSolution2SelectInsert, false);

    [Benchmark]
    public async ValueTask Solution3_AppLockSelectInsert_ToEmpty_Async()
        => await ExecuteTestCaseInTransactionAsync(SqlSolution3AppLockSelectInsert, false);

    [Benchmark]
    public async ValueTask Solution3_AppLockSelectInsert_WhenDataExist_Async()
        => await ExecuteTestCaseInTransactionAsync(SqlSolution3AppLockSelectInsert, false);

    [Benchmark]
    public async ValueTask Solution4_DbLockSelectInsert_ToEmpty_Async()
        => await ExecuteTestCaseInTransactionAsync(SqlSolution4DbLockSelectInsertSelect, false);

    [Benchmark]
    public async ValueTask Solution4_DbLockSelectInsert_WhenDataExist_Async()
        => await ExecuteTestCaseInTransactionAsync(SqlSolution4DbLockSelectInsertSelect, false);

    [Benchmark]
    public void Solution1_InsertSelect_ToEmpty()
        => ExecuteTestCaseInTransaction(SqlSolution1InsertSelect, false);

    [Benchmark]
    public void Solution1_InsertSelect_WhenDataExist()
        => ExecuteTestCaseInTransaction(SqlSolution1InsertSelect, false);

    [Benchmark]
    public void Solution2_SelectInsertSelect_ToEmpty()
        => ExecuteTestCaseInTransaction(SqlSolution2SelectInsert, false);

    [Benchmark]
    public void Solution2_SelectInsertSelect_WhenDataExist()
        => ExecuteTestCaseInTransaction(SqlSolution2SelectInsert, false);

    [Benchmark]
    public void Solution3_AppLockSelectInsert_ToEmpty()
        => ExecuteTestCaseInTransaction(SqlSolution3AppLockSelectInsert, false);

    [Benchmark]
    public void Solution3_AppLockSelectInsert_WhenDataExist()
        => ExecuteTestCaseInTransaction(SqlSolution3AppLockSelectInsert, false);

    [Benchmark]
    public void Solution4_DbLockSelectInsert_ToEmpty()
        => ExecuteTestCaseInTransaction(SqlSolution4DbLockSelectInsertSelect, false);

    [Benchmark]
    public void Solution4_DbLockSelectInsert_WhenDataExist()
        => ExecuteTestCaseInTransaction(SqlSolution4DbLockSelectInsertSelect, false);

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

    private int ExecuteTestCaseInTransaction(string query, bool commit)
    {
        using var transaction = _persistentUserConnection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);

        int result = _persistentUserConnection.Execute(query, _benchmarkBindVars);

        if (commit)
        {
            transaction.Commit();
        }
        else
        {
            transaction.Rollback();
        }

        return result;
    }

    private async Task<int> ExecuteTestCaseInTransactionAsync(string query, bool commit)
    {
        await using var transaction = await _persistentUserConnection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

        int result = await _persistentUserConnection.ExecuteAsync(query, _benchmarkBindVars);

        if (commit)
        {
            await transaction.CommitAsync();
        }
        else
        {
            await transaction.RollbackAsync();
        }

        return result;
    }
}
