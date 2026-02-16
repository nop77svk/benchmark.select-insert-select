namespace DatabaseMultiLockBenchmark.Oracle;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml.Linq;

using DotNet.Testcontainers.Builders;

using Testcontainers.Oracle;

[TestFixture]
public class Benchmarks
    : IAsyncDisposable
{
    private readonly Uri _dockerDaemonUri = new UriBuilder("http", "localhost", 2375).Uri;
    private readonly DotNet.Testcontainers.Containers.IContainer _databaseContainer;

    private bool _disposedValue;

    public Benchmarks()
    {
        _databaseContainer = new ContainerBuilder("container-registry.oracle.com/database/free:latest")
            .WithDockerEndpoint(_dockerDaemonUri)
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .WithPortBinding(1531, 1521)
            .WithEnvironment(new Dictionary<string, string>()
            {
                ["ORACLE_SID"] = "MULTI_LOCK_BENCHMARK_ORACLE",
                ["ORACLE_PDB"] = "MULTI_LOCK_BENCHMARK_TEST",
                ["ORACLE_PWD"] = "Benchmark123",
                ["INIT_SGA_SIZE"] = 2048.ToString(), // MB
                ["INIT_PGA_SIZE"] = 2048.ToString(), // MB
                ["INIT_CPU_COUNT"] = 2.ToString(),
                ["INIT_PROCESSES"] = 200.ToString(),
                ["ORACLE_EDITION"] = "free",
                ["ORACLE_CHARACTERSET"] = "al32utf8",
                ["ENABLE_ARCHIVELOG"] = "false",
                ["ENABLE_FORCE_LOGGING"] = "false",
                ["ENABLE_TCPS"] = "false"
            })
            .Build();
    }

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        await TestContext.Out.WriteLineAsync("Starting database server instance");
        await _databaseContainer.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _databaseContainer.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        await DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

    [SetUp]
    public void Setup()
    {
        // 2do!
    }

    [Test]
    public async Task Test1()
    {
        Assert.Pass();
        await Task.CompletedTask;
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                await _databaseContainer.StopAsync();
                await _databaseContainer.DisposeAsync();
            }

            _disposedValue = true;
        }
    }
}
