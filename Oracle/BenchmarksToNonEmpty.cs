namespace DatabaseMultiLockBenchmark.Oracle;

using System;

using BenchmarkDotNet.Attributes;

[InProcess]
public class BenchmarksToNonEmpty
    : BenchmarksToEmpty
{
    public override void IterationSetup()
    {
        _oracleBenchmarkFunctions.InitialiseTestData();
    }
}
