namespace DatabaseMultiLockBenchmark.Oracle;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

internal class BenchmarksConfig : ManualConfig
{
    public BenchmarksConfig()
    {
        AddJob(Job.InProcess
            .WithMinIterationTime(Perfolizer.Horology.TimeInterval.FromSeconds(1))
            .WithMinIterationCount(100)
            .WithMaxIterationCount(1000)
            .WithMinInvokeCount(100)
            .WithUnrollFactor(15)
            .WithInvocationCount(150)
            .WithMinWarmupCount(100)
            .WithMaxWarmupCount(1000)
        );
    }
}
