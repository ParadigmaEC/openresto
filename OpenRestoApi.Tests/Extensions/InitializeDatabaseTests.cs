using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenRestoApi.Core.Application.Exceptions;
using OpenRestoApi.Core.Application.Interfaces;
using OpenRestoApi.Core.Application.Services;
using OpenRestoApi.Extensions;
using OpenRestoApi.Infrastructure.Persistence;

namespace OpenRestoApi.Tests.Extensions;

/// <summary>
/// <see cref="DatabaseExtensions.InitializeDatabase(WebApplication, string, IConfiguration)"/>
/// against a real, schema-less PostgreSQL database per test, since what it does — migrate, seed,
/// bootstrap the Owner, wait out a database that is still starting — only means anything against
/// a real server.
/// </summary>
public sealed class InitializeDatabaseTests
{
    private const string ClosedPortConnection = "Host=127.0.0.1;Port=1;Database=openresto;Username=openresto;Password=unused;Timeout=2";

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose() { }

        private sealed class CapturingLogger(ConcurrentQueue<(LogLevel, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }

    private static WebApplication BuildApp(
        string connectionString,
        Dictionary<string, string?>? configValues = null,
        CapturingLoggerProvider? logs = null,
        bool retryOnFailure = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        if (logs is not null)
        {
            builder.Logging.AddProvider(logs);
        }

        if (configValues != null)
        {
            builder.Configuration.AddInMemoryCollection(configValues);
        }

        if (retryOnFailure)
        {
            builder.Services.AddDatabaseSetup(connectionString, builder.Environment);
        }
        else
        {
            builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
        }

        builder.Services.AddScoped<IPasswordService, PasswordService>();
        return builder.Build();
    }

    private static Dictionary<string, string?> AdminConfig(string password = "config-password") => new()
    {
        ["Admin:Email"] = "config-admin@openresto.com",
        ["Admin:Password"] = password,
    };

    [Fact]
    public async Task FreshDatabase_AppliesEveryMigration_SeedsRestaurants_AndCreatesTheOwner()
    {
        using PostgresTestDatabase database = PostgresTestDatabase.CreateEmpty();
        await using WebApplication app = BuildApp(database.ConnectionString, AdminConfig());

        app.InitializeDatabase(database.ConnectionString, app.Configuration);

        using IServiceScope scope = app.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
        Assert.True(await db.Restaurants.AnyAsync());
        var owner = await db.AdminCredentials.SingleAsync();
        Assert.Equal("config-admin@openresto.com", owner.Email);
    }

    [Fact]
    public async Task SecondStart_OnAMigratedDatabase_KeepsTheExistingOwner()
    {
        using PostgresTestDatabase database = PostgresTestDatabase.CreateEmpty();
        await using (WebApplication first = BuildApp(database.ConnectionString, AdminConfig()))
        {
            first.InitializeDatabase(database.ConnectionString, first.Configuration);
        }

        await using WebApplication second = BuildApp(database.ConnectionString, new Dictionary<string, string?>
        {
            ["Admin:Email"] = "someone-else@openresto.com",
            ["Admin:Password"] = "another-password",
        });
        second.InitializeDatabase(database.ConnectionString, second.Configuration);

        using IServiceScope scope = second.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.AdminCredentials.SingleAsync();
        Assert.Equal("config-admin@openresto.com", owner.Email);
    }

    [Fact]
    public async Task FreshDatabase_FallsBackToAdminEnvVars()
    {
        using PostgresTestDatabase database = PostgresTestDatabase.CreateEmpty();
        await using WebApplication app = BuildApp(database.ConnectionString);

        Environment.SetEnvironmentVariable("ADMIN_EMAIL", "env-admin@openresto.com");
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", "env-password");
        try
        {
            app.InitializeDatabase(database.ConnectionString, app.Configuration);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ADMIN_EMAIL", null);
            Environment.SetEnvironmentVariable("ADMIN_PASSWORD", null);
        }

        using IServiceScope scope = app.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.AdminCredentials.SingleAsync();
        Assert.Equal("env-admin@openresto.com", owner.Email);
    }

    [Fact]
    public async Task FreshDatabase_Throws_WhenAdminPasswordNotConfigured()
    {
        using PostgresTestDatabase database = PostgresTestDatabase.CreateEmpty();
        await using WebApplication app = BuildApp(database.ConnectionString);
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", null);

        Assert.Throws<InfrastructureException>(() => app.InitializeDatabase(database.ConnectionString, app.Configuration));
    }

    [Fact]
    public async Task StartupDiagnostics_NameTheDatabase_ButNeverThePassword()
    {
        using PostgresTestDatabase database = PostgresTestDatabase.CreateEmpty();
        var logs = new CapturingLoggerProvider();
        await using WebApplication app = BuildApp(database.ConnectionString, AdminConfig(), logs);
        var target = new NpgsqlConnectionStringBuilder(database.ConnectionString);

        app.InitializeDatabase(database.ConnectionString, app.Configuration);

        Assert.Contains(logs.Entries, e => e.Message == $"  - Database: {database.Name} on {target.Host}:{target.Port} as {target.Username}");
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("Password=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnreachableServer_IsRetried_ThenTheFailureIsRethrown()
    {
        var logs = new CapturingLoggerProvider();
        await using WebApplication app = BuildApp(ClosedPortConnection, AdminConfig(), logs, retryOnFailure: false);

        Exception ex = Assert.ThrowsAny<Exception>(() =>
            app.InitializeDatabase(ClosedPortConnection, app.Configuration, maxRetries: 2, retryDelay: TimeSpan.FromMilliseconds(1)));

        Assert.True(DatabaseExtensions.IsTransientStartupFailure(ex));
        Assert.Equal(2, logs.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("Retry", StringComparison.Ordinal)));
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task RejectedCredentials_FailAtOnce_WithoutRetrying()
    {
        using PostgresTestDatabase database = PostgresTestDatabase.CreateEmpty();
        string wrongPassword = new NpgsqlConnectionStringBuilder(database.ConnectionString) { Password = "not-the-password" }.ConnectionString;
        var logs = new CapturingLoggerProvider();
        await using WebApplication app = BuildApp(wrongPassword, AdminConfig(), logs, retryOnFailure: false);

        PostgresException ex = Assert.Throws<PostgresException>(() =>
            app.InitializeDatabase(wrongPassword, app.Configuration, maxRetries: 3, retryDelay: TimeSpan.FromMilliseconds(1)));

        Assert.Equal(PostgresErrorCodes.InvalidPassword, ex.SqlState);
        Assert.DoesNotContain(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Retry", StringComparison.Ordinal));
    }

    [Fact]
    public void IsTransientStartupFailure_WaitsForAServerThatIsStillStarting()
        => Assert.True(DatabaseExtensions.IsTransientStartupFailure(
            new PostgresException("the database system is starting up", "FATAL", "FATAL", PostgresErrorCodes.CannotConnectNow)));

    [Fact]
    public void IsTransientStartupFailure_WaitsForARefusedConnection_EvenWhenWrapped()
        => Assert.True(DatabaseExtensions.IsTransientStartupFailure(
            new InvalidOperationException("wrapper", new SocketException((int)SocketError.ConnectionRefused))));

    [Fact]
    public void IsTransientStartupFailure_DoesNotWaitForRejectedCredentials()
        => Assert.False(DatabaseExtensions.IsTransientStartupFailure(
            new PostgresException("password authentication failed", "FATAL", "FATAL", PostgresErrorCodes.InvalidPassword)));

    [Fact]
    public void IsTransientStartupFailure_DoesNotWaitForAnApplicationError()
        => Assert.False(DatabaseExtensions.IsTransientStartupFailure(new InvalidOperationException("bug")));
}
