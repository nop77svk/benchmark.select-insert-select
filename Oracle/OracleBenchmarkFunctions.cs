namespace DatabaseMultiLockBenchmark.Oracle;
#pragma warning disable SA1116

using System;
using System.IO;
using System.Reflection;

using Dapper;

using global::Oracle.ManagedDataAccess.Client;

public class OracleBenchmarkFunctions
    : IDisposable
{
    internal static readonly string SqlSolution0SelectInsert = ReadSqlTestCaseResource(@"solution0-select-insert.sql");
    internal static readonly string SqlSolution1InsertSelect = ReadSqlTestCaseResource(@"solution1-insert-select.sql");
    internal static readonly string SqlSolution2SelectInsertSelect = ReadSqlTestCaseResource(@"solution2-select-insert-select.sql");
    internal static readonly string SqlSolution3AppLockSelectInsert = ReadSqlTestCaseResource(@"solution3-app-lock-select-insert.sql");
    internal static readonly string SqlSolution4DbLockSelectInsertSelect = ReadSqlTestCaseResource(@"solution4-db-lock-select-insert.sql");

    private const int BindVarA = 1;
    private const int BindVarB = 5;

    private static readonly object _benchmarkBindVars = new { i_a = BindVarA, i_b = BindVarB };
    private readonly OracleConnection _persistentUserConnection;
    private bool _disposedValue;

    public OracleBenchmarkFunctions()
    {
        using (ContainerFactory containerFactory = new ContainerFactory())
        {
            Console.Out.WriteLine($"Connecting to: {containerFactory.UserConnectionString}");
            StaticGlobalContext.UserConnectionString = containerFactory.UserConnectionString;
        }

        _persistentUserConnection = new OracleConnection(StaticGlobalContext.UserConnectionString);
        _persistentUserConnection.Open();
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    internal void PopulateTestTableWithData(int maxRows)
    {
        int maxA = (int)Math.Sqrt(maxRows);
        int maxB = (int)Math.Sqrt(maxRows);

        _persistentUserConnection.Execute("""
            insert --+ append
                into t_data (a, b)
            with A as (
                select --+ no_merge
                    level as a
                from dual
                connect by level <= :i_max_a
            ),
            B as (
                select --+ no_merge
                    level as b
                from dual
                connect by level <= :i_max_b
            )
            select A.a, B.b
            from A cross join B
            where A.a != :i_a or B.b != :i_b
            """,
            new { i_max_a = maxA, i_max_b = maxB, i_a = BindVarA, i_b = BindVarB }
        );
    }

    internal void InitialiseTestData()
    {
        _persistentUserConnection.Execute("""
            merge into t_data T
            using dual S
            on (T.a = :i_a and T.b = :i_b)
            when not matched then
                insert (a, b)
                values (:i_a, :i_b);
            """, _benchmarkBindVars
        );
    }

    internal void TruncateBenchmarkTables()
    {
        _persistentUserConnection.Execute("truncate table t_data_lock drop storage;");
        _persistentUserConnection.Execute("truncate table t_data drop storage;");
    }

    internal int ExecuteTestCaseInTransaction(string query, bool commit)
    {
        using var transaction = _persistentUserConnection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);

        int result = _persistentUserConnection.Execute(query, _benchmarkBindVars);

        if (commit)
        {
            transaction.Commit();
        }
        else
        {
            transaction.Rollback();
        }

        return result;
    }

    internal int ExecuteTestCaseWithAutoRollback(string query)
        => ExecuteTestCaseInTransaction(query, false);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _persistentUserConnection.Close();
                _persistentUserConnection.Dispose();
            }

            _disposedValue = true;
        }
    }

    private static string ReadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ReadSqlTestCaseResource(string testCaseFileName)
        => ReadEmbeddedResource($"{typeof(BenchmarksForInsertOverEmptyTable).Namespace}.TestCases.{testCaseFileName}");
}
