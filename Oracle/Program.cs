namespace Oracle;

using System;
using System.Collections.Generic;
using System.Text;

using BenchmarkDotNet.Running;

using DatabaseMultiLockBenchmark.Oracle;

internal static class Program
{
    internal static void Main(string[] args)
    {
        BenchmarkRunner.Run<Benchmarks>();
    }
}
