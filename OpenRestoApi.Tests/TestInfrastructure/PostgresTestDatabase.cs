using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenRestoApi.Infrastructure.Persistence;

namespace OpenRestoApi.Tests.TestInfrastructure;

/// <summary>
/// A migrated, empty PostgreSQL database for one test (or one <c>TestWebAppFactory</c>).
/// <para>
/// <c>CREATE DATABASE</c> costs seconds on the local server (PostgreSQL 14 checkpoints and
/// fsyncs the template on every copy), and xUnit builds a test class once per test, so a
/// database per lease would add minutes per run. Databases are therefore pooled for the
/// lifetime of the test process instead: <see cref="Acquire"/> hands out an idle one after
/// deleting every row and restarting identities, which is the state a freshly migrated
/// database is in, and disposing the lease returns it. They are dropped when the process exits,
/// and a database left behind by a process that no longer exists is dropped by the next run.
/// </para>
/// <para>
/// The server comes from <see cref="AdminConnectionEnvVar"/>; the role it names needs
/// <c>CREATEDB</c>. There is no skip: an unreachable server fails the test with the variable's
/// name in the message.
/// </para>
/// </summary>
public sealed class PostgresTestDatabase : IDisposable
{
    public const string AdminConnectionEnvVar = "OPENRESTO_TEST_POSTGRES";
    private const string DefaultAdminConnection =
        "Host=localhost;Port=5432;Username=openresto;Password=openresto;Database=postgres";
    private const string NamePrefix = "openresto_test_";

    private static readonly ConcurrentBag<string> Idle = [];
    private static readonly ConcurrentDictionary<string, byte> Created = new();
    private static readonly Lazy<bool> Started = new(StartRun);

    private readonly bool _pooled;
    private bool _disposed;

    private PostgresTestDatabase(string name, bool pooled)
    {
        Name = name;
        _pooled = pooled;
        ConnectionString = ConnectionStringFor(name);
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public DbContextOptions<AppDbContext> Options => new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(ConnectionString)
        .Options;

    /// <summary>A migrated database with no rows in it, returned to the pool on dispose.</summary>
    public static PostgresTestDatabase Acquire()
    {
        _ = Started.Value;
        if (Idle.TryTake(out string? name))
        {
            var reused = new PostgresTestDatabase(name, pooled: true);
            reused.Reset();
            return reused;
        }

        var created = new PostgresTestDatabase(CreateDatabase(), pooled: true);
        using (AppDbContext db = created.CreateContext())
        {
            db.Database.Migrate();
        }

        return created;
    }

    /// <summary>
    /// A database with no schema at all, for tests of the startup path that migrates it. Never
    /// pooled: it is dropped on dispose.
    /// </summary>
    public static PostgresTestDatabase CreateEmpty()
    {
        _ = Started.Value;
        return new PostgresTestDatabase(CreateDatabase(), pooled: false);
    }

    public AppDbContext CreateContext() => new(Options);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_pooled)
        {
            Idle.Add(Name);
        }
        else
        {
            Drop(Name);
        }
    }

    private string ConnectionStringFor(string database)
        => new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Database = database,
            MaxPoolSize = 30,
        }.ConnectionString;

    /// <summary>
    /// Deletes every row, children before parents, and restarts every identity at 1. Not
    /// <c>TRUNCATE</c>: that swaps in new files for all the tables and fsyncs them, which on the
    /// local server costs about a second per test against a few milliseconds for deleting the
    /// handful of rows a test leaves behind.
    /// </summary>
    private void Reset()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        ResetSql ??= BuildResetSql(connection);
        using var reset = new NpgsqlCommand(ResetSql, connection);
        reset.ExecuteNonQuery();
    }

    private static string? ResetSql;

    private static string BuildResetSql(NpgsqlConnection connection)
    {
        var tables = new List<string>();
        using (var list = new NpgsqlCommand(
            "SELECT format('%I', tablename) FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'",
            connection))
        using (NpgsqlDataReader reader = list.ExecuteReader())
        {
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        var references = new List<(string Child, string Parent)>();
        using (var list = new NpgsqlCommand(
            "SELECT format('%I', c.relname), format('%I', p.relname) FROM pg_constraint k "
            + "JOIN pg_class c ON c.oid = k.conrelid JOIN pg_class p ON p.oid = k.confrelid "
            + "WHERE k.contype = 'f' AND k.connamespace = 'public'::regnamespace AND k.conrelid <> k.confrelid",
            connection))
        using (NpgsqlDataReader reader = list.ExecuteReader())
        {
            while (reader.Read())
            {
                references.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        var identities = new List<(string Table, string Column)>();
        using (var list = new NpgsqlCommand(
            "SELECT format('%I', table_name), column_name FROM information_schema.columns "
            + "WHERE table_schema = 'public' AND is_identity = 'YES'",
            connection))
        using (NpgsqlDataReader reader = list.ExecuteReader())
        {
            while (reader.Read())
            {
                identities.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        var sql = new System.Text.StringBuilder();
        var remaining = new List<string>(tables);
        while (remaining.Count > 0)
        {
            List<string> leaves = remaining
                .Where(t => !references.Any(r => r.Parent == t && r.Child != t && remaining.Contains(r.Child)))
                .ToList();
            if (leaves.Count == 0)
            {
                throw new InvalidOperationException("The schema has a foreign-key cycle the test reset cannot order.");
            }

            foreach (string table in leaves)
            {
                sql.Append(CultureInfo.InvariantCulture, $"DELETE FROM {table};");
                remaining.Remove(table);
            }
        }

        foreach ((string table, string column) in identities)
        {
            string literal = table.Replace("'", "''", StringComparison.Ordinal);
            sql.Append(CultureInfo.InvariantCulture, $"SELECT setval(pg_get_serial_sequence('{literal}', '{column}'), 1, false);");
        }

        return sql.ToString();
    }

    private static string AdminConnectionString
        => Environment.GetEnvironmentVariable(AdminConnectionEnvVar) is { Length: > 0 } configured
            ? configured
            : DefaultAdminConnection;

    private static string CreateDatabase()
    {
        string name = string.Create(
            CultureInfo.InvariantCulture,
            $"{NamePrefix}{Environment.ProcessId}_{Guid.NewGuid().ToString("N")[..12]}");
        RunAdmin($"CREATE DATABASE \"{name}\" ENCODING 'UTF8' TEMPLATE template0");
        Created[name] = 0;
        return name;
    }

    /// <summary>
    /// Best effort: a database still in use after a few seconds is left for the next run's
    /// <see cref="DropDatabasesOfDeadRuns"/> rather than failing the test that released it.
    /// <c>WITH (FORCE)</c> is not used because it also tries to terminate autovacuum, which a role
    /// without <c>pg_signal_backend</c> is refused.
    /// </summary>
    private static void Drop(string name)
    {
        NpgsqlConnection.ClearAllPools();
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                RunAdmin($"DROP DATABASE IF EXISTS \"{name}\"");
                Created.TryRemove(name, out _);
                return;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ObjectInUse && attempt < 20)
            {
                Thread.Sleep(250);
            }
            catch (PostgresException)
            {
                return;
            }
        }
    }

    private static bool StartRun()
    {
        DropDatabasesOfDeadRuns();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            foreach (string name in Created.Keys)
            {
                try
                {
                    Drop(name);
                }
                catch (NpgsqlException)
                {
                    // Left for the next run's DropDatabasesOfDeadRuns.
                }
            }
        };
        return true;
    }

    private static void DropDatabasesOfDeadRuns()
    {
        var stale = new List<string>();
        using (NpgsqlConnection connection = OpenAdmin())
        using (var command = new NpgsqlCommand(
            $"SELECT datname FROM pg_database WHERE datname LIKE '{NamePrefix}%'", connection))
        using (NpgsqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                string name = reader.GetString(0);
                if (!IsOwnedByLiveProcess(name))
                {
                    stale.Add(name);
                }
            }
        }

        foreach (string name in stale)
        {
            Drop(name);
        }
    }

    private static bool IsOwnedByLiveProcess(string name)
    {
        string[] parts = name[NamePrefix.Length..].Split('_');
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int pid))
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void RunAdmin(string sql)
    {
        using NpgsqlConnection connection = OpenAdmin();
        // CREATE DATABASE waits on a checkpoint, which can outlast the default 30 seconds.
        using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        command.ExecuteNonQuery();
    }

    private static NpgsqlConnection OpenAdmin()
    {
        var connection = new NpgsqlConnection(AdminConnectionString);
        try
        {
            connection.Open();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"The backend tests need a PostgreSQL server they can create databases on. Could not connect "
                + $"with {AdminConnectionEnvVar} (or its default, {Redacted(DefaultAdminConnection)}): {ex.Message}",
                ex);
        }

        return connection;
    }

    private static string Redacted(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Password = null };
        return builder.ConnectionString;
    }
}
