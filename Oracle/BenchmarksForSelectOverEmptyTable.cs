namespace DatabaseMultiLockBenchmark.Oracle;

using BenchmarkDotNet.Attributes;

[Config(typeof(BenchmarksConfig))]
public class BenchmarksForSelectOverEmptyTable
    : BenchmarksForInsertOverEmptyTable
{
    public override void IterationSetup()
    {
        _oracleBenchmarkFunctions.InitialiseTestData();
    }
}
