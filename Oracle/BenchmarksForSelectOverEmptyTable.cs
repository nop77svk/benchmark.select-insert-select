namespace DatabaseMultiLockBenchmark.Oracle;

using BenchmarkDotNet.Attributes;

[InProcess]
public class BenchmarksForSelectOverEmptyTable
    : BenchmarksForInsertOverEmptyTable
{
    public override void IterationSetup()
    {
        _oracleBenchmarkFunctions.InitialiseTestData();
    }
}
