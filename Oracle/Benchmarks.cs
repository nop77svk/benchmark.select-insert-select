namespace DatabaseMultiLockBenchmark.Oracle;
#pragma warning disable SA1116

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using Dapper;

using global::Oracle.ManagedDataAccess.Client;

[InProcess]
public class Benchmarks
    : IDisposable
{
    private static readonly string _sqlSolution1InsertSelect = ReadSqlTestCaseResource(@"solution1-insert-select.sql");
    private static readonly string _sqlSolution2SelectInsertSelect = ReadSqlTestCaseResource(@"solution2-select-insert-select.sql");
    private static readonly string _sqlSolution3AppLockSelectInsert = ReadSqlTestCaseResource(@"solution3-app-lock-select-insert.sql");
    private static readonly string _sqlSolution4DbLockSelectInsertSelect = ReadSqlTestCaseResource(@"solution4-db-lock-select-insert.sql");

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
        _persistentUserConnection.Execute("""
            merge into t_data T
            using dual S
            on (T.a = :i_a and T.b = :i_b)
            when not matched then
                insert (a, b)
                values (:i_a, :i_b);
            """, _benchmarkBindVars
        );
    }

    [Benchmark]
    public async ValueTask Solution1_InsertSelect_ToEmpty_Async()
        => await ExecuteTestCaseInTransactionAsync(_sqlSolution1InsertSelect, false);

    [Benchmark]
    public async ValueTask Solution1_InsertSelect_WhenDataExist_Async()
        => await ExecuteTestCaseInTransactionAsync(_sqlSolution1InsertSelect, false);

    [Benchmark]
    public async ValueTask Solution2_SelectInsertSelect_ToEmpty_Async()
        => await ExecuteTestCaseInTransactionAsync(_sqlSolution2SelectInsertSelect, false);

    [Benchmark]
    public async ValueTask Solution2_SelectInsertSelect_WhenDataExist_Async()
        => await ExecuteTestCaseInTransactionAsync(_sqlSolution2SelectInsertSelect, false);

    [Benchmark]
    public async ValueTask Solution3_AppLockSelectInsert_ToEmpty_Async()
        => await ExecuteTestCaseInTransactionAsync(_sqlSolution3AppLockSelectInsert, false);

    [Benchmark]
    public async ValueTask Solution3_AppLockSelectInsert_WhenDataExist_Async()
        => await ExecuteTestCaseInTransactionAsync(_sqlSolution3AppLockSelectInsert, false);

    [Benchmark]
    public async ValueTask Solution4_DbLockSelectInsert_ToEmpty_Async()
        => await ExecuteTestCaseInTransactionAsync(_sqlSolution4DbLockSelectInsertSelect, false);

    [Benchmark]
    public async ValueTask Solution4_DbLockSelectInsert_WhenDataExist_Async()
        => await ExecuteTestCaseInTransactionAsync(_sqlSolution4DbLockSelectInsertSelect, false);

    [Benchmark]
    public void Solution1_InsertSelect_ToEmpty()
        => ExecuteTestCaseInTransaction(_sqlSolution1InsertSelect, false);

    [Benchmark]
    public void Solution1_InsertSelect_WhenDataExist()
        => ExecuteTestCaseInTransaction(_sqlSolution1InsertSelect, false);

    [Benchmark]
    public void Solution2_SelectInsertSelect_ToEmpty()
        => ExecuteTestCaseInTransaction(_sqlSolution2SelectInsertSelect, false);

    [Benchmark]
    public void Solution2_SelectInsertSelect_WhenDataExist()
        => ExecuteTestCaseInTransaction(_sqlSolution2SelectInsertSelect, false);

    [Benchmark]
    public void Solution3_AppLockSelectInsert_ToEmpty()
        => ExecuteTestCaseInTransaction(_sqlSolution3AppLockSelectInsert, false);

    [Benchmark]
    public void Solution3_AppLockSelectInsert_WhenDataExist()
        => ExecuteTestCaseInTransaction(_sqlSolution3AppLockSelectInsert, false);

    [Benchmark]
    public void Solution4_DbLockSelectInsert_ToEmpty()
        => ExecuteTestCaseInTransaction(_sqlSolution4DbLockSelectInsertSelect, false);

    [Benchmark]
    public void Solution4_DbLockSelectInsert_WhenDataExist()
        => ExecuteTestCaseInTransaction(_sqlSolution4DbLockSelectInsertSelect, false);

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

    private static string ReadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ReadSqlTestCaseResource(string testCaseFileName)
        => ReadEmbeddedResource($"{typeof(Benchmarks).Namespace}.TestCases.{testCaseFileName}");

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
