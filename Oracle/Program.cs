namespace DatabaseMultiLockBenchmark.Oracle;

using System;
using System.Threading.Tasks;

using BenchmarkDotNet.Running;

internal static class Program
{
    internal static async Task Main(string[] args)
    {
        await using ContainerFactory containerFactory = new ContainerFactory();
        await using IAsyncDisposable containerStarted = await containerFactory.StartUpContainer();

        StaticGlobalContext.UserConnectionString = containerFactory.UserConnectionString;
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
            .RunAllJoined();
    }
}
