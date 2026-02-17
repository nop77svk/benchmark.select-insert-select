namespace DatabaseMultiLockBenchmark.Oracle;

using BenchmarkDotNet.Attributes;

[Config(typeof(BenchmarksConfig))]
public class BenchmarksForSelectOverHugeTable
    : BenchmarksForSelectOverEmptyTable
{
    public override void BenchmarkSetUp()
    {
        base.BenchmarkSetUp();
        _oracleBenchmarkFunctions.PopulateTestTableWithData(10000000);
    }
}
