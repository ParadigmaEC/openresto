using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenRestoApi.Core.Application.Interfaces;
using OpenRestoApi.Core.Application.Utilities;
using OpenRestoApi.Core.Domain;
using OpenRestoApi.Infrastructure.Persistence;

namespace OpenRestoApi.Tests.Integration;

public class TestWebAppFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.com";
    public const string AdminPassword = "TestPass123!";
    public const string JwtKey = "test-jwt-signing-key-for-integration-tests-minimum-32-chars!!";
    public const string JwtIssuer = "openresto-api";
    public const string JwtAudience = "openresto-admin";

    private readonly PostgresTestDatabase _database = PostgresTestDatabase.Acquire();

    /// <summary>The database this factory's app runs against, for tests that open their own contexts.</summary>
    public string ConnectionString => _database.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _database.ConnectionString);

        builder.ConfigureServices(services =>
        {
            // Replace IEmailService with a mock for testing
            ServiceDescriptor? emailServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailServiceDescriptor != null)
            {
                services.Remove(emailServiceDescriptor);
            }
            services.AddScoped<IEmailService, MockEmailService>();

            // Replace INotificationQueue with a no-op so no background send races a test's
            // assertions or outlives the factory's database lease.
            ServiceDescriptor? queueDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(INotificationQueue));
            if (queueDescriptor != null)
                services.Remove(queueDescriptor);
            services.AddSingleton<INotificationQueue, NoOpNotificationQueue>();

        });

        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Admin:Email", AdminEmail);
        builder.UseSetting("Admin:Password", AdminPassword);
        builder.UseSetting("Cors:Origins", "http://localhost");
    }

    /// <summary>
    /// Generates a valid JWT for the seeded admin, in the shape the current build mints:
    /// user id + email + the account's own role.
    /// </summary>
    public string GenerateTestJwt()
    {
        AdminCredential admin = GetSeededAdmin();
        return GenerateJwt(admin.Id, admin.Email, admin.Role);
    }

    /// <summary>
    /// A token in the shape a pre-multi-user build minted: no <c>sub</c> claim and the retired
    /// <c>Admin</c> role. Backward compatibility for these is why services fall back to the email
    /// claim and why both policies accept <see cref="UserRoles.LegacyAdmin"/>.
    /// </summary>
    public static string GenerateLegacyJwt(string email = AdminEmail)
        => BuildToken([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, UserRoles.LegacyAdmin)]);

    public static string GenerateJwt(int userId, string email, string role)
        => BuildToken(
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
        ]);

    private static string BuildToken(Claim[] claims)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(JwtKey);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>The account created by the first-run bootstrap — an Owner.</summary>
    public AdminCredential GetSeededAdmin()
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.AdminCredentials.OrderBy(c => c.Id).First();
    }

    /// <summary>
    /// Creates an HttpClient with a valid JWT Authorization header.
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        return CreateClientWithToken(GenerateTestJwt());
    }

    public HttpClient CreateClientWithToken(string jwt)
    {
        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _database.Dispose();
        }
    }

    private sealed class MockEmailService(AppDbContext db) : IEmailService
    {
        private readonly AppDbContext _db = db;
        public async Task<bool> TestConnectionAsync() => await _db.Set<OpenRestoApi.Core.Domain.EmailSettings>().AnyAsync();
        public Task SendEmailAsync(string recipient, string subject, string htmlBody) => Task.CompletedTask;
    }

    private sealed class NoOpNotificationQueue : INotificationQueue
    {
        public void EnqueueBookingCreated(Booking booking, string restaurantName) { }
        public void EnqueueBookingCancelled(Booking booking, string restaurantName) { }
        public void EnqueueCapacityCheck(int restaurantId, string restaurantName, DateTime bookingDate) { }
    }
}
