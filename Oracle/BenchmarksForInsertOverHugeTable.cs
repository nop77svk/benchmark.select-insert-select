namespace DatabaseMultiLockBenchmark.Oracle;

using BenchmarkDotNet.Attributes;

internal class BenchmarksForInsertOverHugeTable
    : BenchmarksForInsertOverEmptyTable
{
    [GlobalSetup]
    public override void BenchmarkSetUp()
    {
        _oracleBenchmarkFunctions.PopulateTestTableWithData(1000, 1000);
    }
}
