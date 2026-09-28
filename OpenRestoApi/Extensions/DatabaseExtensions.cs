using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using CustomAccessibility.Attributes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenRestoApi.Infrastructure.Persistence;

namespace OpenRestoApi.Extensions;

public static partial class DatabaseExtensions
{
    public const string ConnectionStringKey = "ConnectionStrings:DefaultConnection";
    public const string ConnectionStringEnvVar = "CONNECTION_STRING";

    internal const int StartupMaxRetries = 10;
    internal static readonly TimeSpan StartupRetryDelay = TimeSpan.FromSeconds(2);

    [LoggerMessage(Level = LogLevel.Information, Message = "Startup Diagnostics:")]
    private static partial void LogStartupDiagnostics(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "  - Database: {Database} on {Host}:{Port} as {Username}")]
    private static partial void LogDatabaseTarget(ILogger logger, string? database, string? host, int port, string? username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database not reachable yet ({Reason}). Retry {RetryCount}/{MaxRetries} in {Delay}ms...")]
    private static partial void LogDatabaseRetry(ILogger logger, string reason, int retryCount, int maxRetries, int delay);

    [LoggerMessage(Level = LogLevel.Critical, Message = "FATAL ERROR during database initialization. The application cannot start.")]
    private static partial void LogFatalError(ILogger logger, Exception ex);

    public static string GetAppConnectionString(this IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable(ConnectionStringEnvVar);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"No PostgreSQL connection string configured. Set '{ConnectionStringKey}' in configuration "
                + $"or the {ConnectionStringEnvVar} environment variable.");
        }

        return connectionString;
    }

    public static IServiceCollection AddDatabaseSetup(this IServiceCollection services, string connectionString, IWebHostEnvironment env)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.CommandTimeout(30);
                npgsql.EnableRetryOnFailure();
            });
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning));
            options.EnableSensitiveDataLogging(env.IsDevelopment());
            options.EnableDetailedErrors(env.IsDevelopment());
        });

        return services;
    }

    public static void InitializeDatabase(this WebApplication app, string connectionString, IConfiguration configuration)
        => app.InitializeDatabase(connectionString, configuration, StartupMaxRetries, StartupRetryDelay);

    [OnlyAccessibleBy("OpenRestoApi.Extensions.*")]
    [OnlyAccessibleBy("OpenRestoApi.Tests.Extensions.InitializeDatabaseTests")]
    [ExternalAccessAllowed]
    internal static void InitializeDatabase(
        this WebApplication app,
        string connectionString,
        IConfiguration configuration,
        int maxRetries,
        TimeSpan retryDelay)
    {
        using IServiceScope scope = app.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            LogStartupDiagnostics(logger);
            LogTarget(logger, connectionString);

            bool success = false;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    ApplySchema(db);
                    DbSeeder.Seed(db);
                    AdminBootstrap.EnsureInitialOwner(
                        db,
                        configuration,
                        scope.ServiceProvider.GetRequiredService<OpenRestoApi.Core.Application.Interfaces.IPasswordService>());

                    success = true;
                    break;
                }
                catch (Exception ex) when (IsTransientStartupFailure(ex))
                {
                    LogDatabaseRetry(logger, ex.GetBaseException().Message, attempt, maxRetries, (int)retryDelay.TotalMilliseconds);
                    if (attempt == maxRetries)
                    {
                        throw;
                    }

                    Thread.Sleep(retryDelay);
                }
            }

            ThrowIfRetriesExhausted(success);
        }
        catch (Exception ex)
        {
            LogFatalError(logger, ex);
            throw;
        }
    }

    /// <summary>
    /// Migrations on PostgreSQL. The in-memory provider has no migrations and is only ever reached
    /// by <c>tools/OpenApiExport</c>, which boots the app to read its contract without a database.
    /// </summary>
    private static void ApplySchema(AppDbContext db)
    {
        if (db.Database.IsRelational())
        {
            db.Database.Migrate();
        }
        else
        {
            db.Database.EnsureCreated();
        }
    }

    /// <summary>
    /// Only the host and database are logged: the connection string carries the password.
    /// </summary>
    private static void LogTarget(ILogger logger, string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        LogDatabaseTarget(logger, builder.Database, builder.Host, builder.Port, builder.Username);
    }

    /// <summary>
    /// A database that is still starting (compose brings the backend up beside it) refuses
    /// connections or answers <c>57P03 cannot_connect_now</c>; both clear on their own, so startup
    /// waits rather than failing. Anything else, a bad password included, fails at once.
    /// </summary>
    [OnlyAccessibleBy("OpenRestoApi.Extensions.*")]
    [OnlyAccessibleBy("OpenRestoApi.Tests.Extensions.InitializeDatabaseTests")]
    [ExternalAccessAllowed]
    internal static bool IsTransientStartupFailure(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is SocketException || (current is NpgsqlException npgsql && npgsql.IsTransient))
            {
                return true;
            }
        }

        return false;
    }

    // The loop above can only fall through to here with success == true: every transient failure
    // either sleeps and continues (attempt < maxRetries) or rethrows (attempt == maxRetries), and
    // anything else propagates immediately.
    [ExcludeFromCodeCoverage(Justification = "Unreachable: the retry loop above always either sets success=true or throws before falling through.")]
    private static void ThrowIfRetriesExhausted(bool success)
    {
        if (!success)
        {
            throw new InvalidOperationException("Failed to initialize database after multiple retries.");
        }
    }
}
