namespace DatabaseMultiLockBenchmark.Oracle;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Dapper;

using global::Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

public class ContainerFactory
    : IAsyncDisposable, IDisposable
{
    public string DockerDaemonHost { get; init; } = "localhost";
    public int DockerDaemonPort { get; init; } = 2375;

    public string OracleDockerImageSource { get; init; } = "container-registry.oracle.com/database/free:latest-lite";
    public int OracleContainerHostPort { get; init; } = 1522;
    public string OracleContainerPassword { get; init; } = Guid.NewGuid().ToString();
    public string OracleBenchmarkUserName { get; init; } = "BENCHMARK_USER";
    public string OracleBenchmarkUserPassword { get; init; } = "Benchmark123";

    public Uri DockerDaemonUri => new UriBuilder("http", DockerDaemonHost, DockerDaemonPort).Uri;

    public string DbaConnectionString => _dbaConnectionStringBuilder.Value.ConnectionString;
    public string UserConnectionString => _userConnectionStringBuilder.Value.ConnectionString;

    private readonly Lazy<OracleContainer> _databaseContainer;
    private readonly Lazy<OracleConnectionStringBuilder> _dbaConnectionStringBuilder;
    private readonly Lazy<OracleConnectionStringBuilder> _userConnectionStringBuilder;

    public ContainerFactory()
    {
        _databaseContainer = new(() => new OracleBuilder(OracleDockerImageSource)
            .WithDockerEndpoint(DockerDaemonUri)
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .WithPortBinding(OracleContainerHostPort, 1521)
            .WithEnvironment(new Dictionary<string, string>()
            {
                ["ORACLE_PWD"] = OracleContainerPassword,
                ["ORACLE_CHARACTERSET"] = "al32utf8",
                ["ENABLE_ARCHIVELOG"] = "false",
                ["ENABLE_FORCE_LOGGING"] = "false"
            })
            .Build()
        );

        _dbaConnectionStringBuilder = new(() => new OracleConnectionStringBuilder()
        {
            DataSource = $"127.0.0.1:{OracleContainerHostPort}/FREEPDB1",
            UserID = "SYS",
            Password = OracleContainerPassword,
            DBAPrivilege = "SYSDBA"
        });

        _userConnectionStringBuilder = new(() => new OracleConnectionStringBuilder()
        {
            DataSource = $"127.0.0.1:{OracleContainerHostPort}/FREEPDB1",
            UserID = OracleBenchmarkUserName,
            Password = OracleBenchmarkUserPassword
        });
    }

    private bool _disposedValue;

    public async Task<IAsyncDisposable> StartUpContainer()
    {
        await Console.Out.WriteLineAsync("Starting database server instance");
        await _databaseContainer.Value.StartAsync();

        await Console.Out.WriteLineAsync("Setting up benchmark DB schema");

        Console.WriteLine($"Connecting to: {DbaConnectionString}");
        await using (var dbaConnection = new OracleConnection(DbaConnectionString))
        {
            await dbaConnection.OpenAsync();

            await Console.Out.WriteLineAsync(" * Create tablespace");
            await dbaConnection.ExecuteAsync("""
                create smallfile tablespace benchmark_tbs
                datafile '/opt/oracle/oradata/FREE/FREEPDB1/benchmark_tbs_01.dbf' size 16m
                autoextend on next 16m maxsize unlimited
                segment space management auto
                extent management local autoallocate;
            """);

            await Console.Out.WriteLineAsync(" * Create test user");
            await dbaConnection.ExecuteAsync($"""
                create user "{OracleBenchmarkUserName}"
                    identified by "{OracleBenchmarkUserPassword}"
                default tablespace benchmark_tbs
                quota unlimited on benchmark_tbs
                temporary tablespace temp
                account unlock;
            """);

            await dbaConnection.ExecuteAsync($"""
                grant create session, create table, create sequence, create procedure
                to {OracleBenchmarkUserName};
            """);

            await dbaConnection.ExecuteAsync($"""
                grant execute on sys.dbms_lock
                to {OracleBenchmarkUserName};
            """);
        }

        Console.WriteLine($"Connecting to: {UserConnectionString}");
        await using (var userConnection = new OracleConnection(UserConnectionString))
        {
            await userConnection.OpenAsync();

            await Console.Out.WriteLineAsync(" * Create data table");
            await userConnection.ExecuteAsync($"""
                create table t_data
                (
                    id              integer generated always as identity not null,
                    constraint PK_data primary key (id),
                    a               integer not null,
                    b               integer not null,
                    constraint PK_data_2 unique (a, b) using index
                );
            """);

            await Console.Out.WriteLineAsync(" * Create locks table");
            await userConnection.ExecuteAsync($"""
                create table t_data_lock
                (
                    a               integer not null,
                    b               integer not null,
                    constraint PK_data_locks primary key (a, b)
                )
                organization index;
            """);

            await userConnection.CloseAsync();
        }

        await Console.Out.WriteLineAsync("DB instance prepared!");

        return new AsyncAutoDisposer(async () => await ShutDownContainer());
    }

    public async Task ShutDownContainer()
    {
        await _databaseContainer.Value.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        await DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                await _databaseContainer.Value.DisposeAsync();
            }

            _disposedValue = true;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _databaseContainer.Value.DisposeAsync().GetAwaiter().GetResult();
            }

            _disposedValue = true;
        }
    }
}
