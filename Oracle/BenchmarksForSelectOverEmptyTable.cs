namespace DatabaseMultiLockBenchmark.Oracle;

using BenchmarkDotNet.Attributes;

[InProcess]
public class BenchmarksForSelectOverEmptyTable
    : BenchmarksForInsertOverEmptyTable
{
    [IterationSetup]
    public override void IterationSetup()
    {
        _oracleBenchmarkFunctions.InitialiseTestData();
    }
}
