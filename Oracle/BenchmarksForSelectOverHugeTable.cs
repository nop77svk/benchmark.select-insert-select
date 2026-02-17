namespace DatabaseMultiLockBenchmark.Oracle;

using BenchmarkDotNet.Attributes;

[InProcess]
public class BenchmarksForSelectOverHugeTable
    : BenchmarksForSelectOverEmptyTable
{
    public override void BenchmarkSetUp()
    {
        base.BenchmarkSetUp();
        _oracleBenchmarkFunctions.PopulateTestTableWithData(10000, 10000);
    }
}
