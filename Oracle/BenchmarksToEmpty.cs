namespace DatabaseMultiLockBenchmark.Oracle;
#pragma warning disable SA1401 // Field should be private

using System;
using System.Threading;

using BenchmarkDotNet.Attributes;

[InProcess]
public class BenchmarksToEmpty
    : IDisposable
{
    protected readonly OracleBenchmarkFunctions _oracleBenchmarkFunctions;

    private bool _disposedValue;

    public BenchmarksToEmpty()
    {
        _oracleBenchmarkFunctions = new OracleBenchmarkFunctions();
    }

    [GlobalSetup]
    public void BenchmarkSetUp()
    {
        _oracleBenchmarkFunctions.TruncateBenchmarkTables();
    }

    [GlobalCleanup]
    public void BenchmarkTearDown()
    {
        _oracleBenchmarkFunctions.TruncateBenchmarkTables();
    }

    public virtual void IterationSetup()
    {
    }

    [Benchmark]
    public void Solution0_TheWrongOne_SelectInsert()
        => _oracleBenchmarkFunctions.ExecuteTestCaseWithAutoRollback(OracleBenchmarkFunctions.SqlSolution0SelectInsert);

    private static readonly SemaphoreSlim _solution0Semaphore = new SemaphoreSlim(1);

    [Benchmark]
    public void Solution1_InsertSelect()
        => _oracleBenchmarkFunctions.ExecuteTestCaseWithAutoRollback(OracleBenchmarkFunctions.SqlSolution1InsertSelect);

    [Benchmark(Baseline = true)]
    public void Solution2_SelectInsertSelect()
        => _oracleBenchmarkFunctions.ExecuteTestCaseWithAutoRollback(OracleBenchmarkFunctions.SqlSolution2SelectInsertSelect);

    [Benchmark]
    public void Solution3_AppLockSelectInsert()
        => _oracleBenchmarkFunctions.ExecuteTestCaseWithAutoRollback(OracleBenchmarkFunctions.SqlSolution3AppLockSelectInsert);

    [Benchmark]
    public void Solution4_DbLockSelectInsert()
        => _oracleBenchmarkFunctions.ExecuteTestCaseWithAutoRollback(OracleBenchmarkFunctions.SqlSolution4DbLockSelectInsertSelect);

    [Benchmark]
    public void Solution5_SelectInsertWithDotNetLocking()
    {
        _solution0Semaphore.Wait();
        try
        {
            _oracleBenchmarkFunctions.ExecuteTestCaseWithAutoRollback(OracleBenchmarkFunctions.SqlSolution0SelectInsert);
        }
        finally
        {
            _solution0Semaphore.Release();
        }
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
                _oracleBenchmarkFunctions.Dispose();
            }

            _disposedValue = true;
        }
    }
}
