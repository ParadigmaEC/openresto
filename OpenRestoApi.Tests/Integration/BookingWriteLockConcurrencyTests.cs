using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenRestoApi.Core.Application.Utilities;
using OpenRestoApi.Core.Domain;
using OpenRestoApi.Infrastructure.Persistence;

namespace OpenRestoApi.Tests.Integration;

/// <summary>
/// Real races against PostgreSQL: several requests for the same furniture or the same covers
/// released at once through the HTTP API, each in its own request scope and connection. Without
/// the per-restaurant write lock more than one of them passes its check before any has written,
/// and the table is booked twice. No holds are involved, so the lock is the only thing that can
/// make these pass. Each test books its own day so the tests cannot see each other's rows.
/// </summary>
public sealed class BookingWriteLockConcurrencyTests : IClassFixture<TestWebAppFactory>
{
    private const int Racers = 8;

    private readonly TestWebAppFactory _factory;

    public BookingWriteLockConcurrencyTests(TestWebAppFactory factory) => _factory = factory;

    private sealed record Floor(int RestaurantId, int SectionId, int FourTop, int TwoTop, int OtherRestaurantId, int OtherSectionId, int OtherTable);

    private Floor SeededFloor()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Restaurant first = db.Restaurants.OrderBy(r => r.Id).First();
        Restaurant second = db.Restaurants.OrderBy(r => r.Id).Skip(1).First();
        Section indoor = db.Sections.Where(s => s.RestaurantId == first.Id).OrderBy(s => s.Id).First();
        List<Table> indoorTables = db.Tables.Where(t => t.SectionId == indoor.Id).OrderBy(t => t.Id).ToList();
        Section bar = db.Sections.First(s => s.RestaurantId == second.Id);
        Table barTable = db.Tables.OrderBy(t => t.Id).First(t => t.SectionId == bar.Id);
        return new Floor(
            first.Id, indoor.Id, indoorTables.First(t => t.Seats == 4).Id, indoorTables.First(t => t.Seats == 2).Id,
            second.Id, bar.Id, barTable.Id);
    }

    private static string DayAt(int daysAhead, int hour = 19)
        => DateTime.UtcNow.Date.AddDays(daysAhead).AddHours(hour).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static object AdminBooking(Floor floor, int tableId, string date, int seats = 2) => new
    {
        restaurantId = floor.RestaurantId,
        sectionId = floor.SectionId,
        tableId,
        date,
        seats,
        customerEmail = "racer@test.com",
        customerPhone = TestPhones.Valid,
    };

    private static object GuestBooking(int restaurantId, int sectionId, int? tableId, string date, int seats = 2, int? tableGroupId = null) => new
    {
        restaurantId,
        sectionId,
        tableId,
        tableGroupId,
        date,
        seats,
        customerEmail = "racer@test.com",
        customerPhone = TestPhones.Valid,
    };

    /// <summary>Releases every request at once and waits for all of them.</summary>
    private static async Task<List<HttpResponseMessage>> RaceAsync(IEnumerable<Func<Task<HttpResponseMessage>>> requests)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        List<Task<HttpResponseMessage>> running = requests
            .Select(send => Task.Run(async () =>
            {
                await start.Task;
                return await send();
            }))
            .ToList();
        start.SetResult();
        return [.. await Task.WhenAll(running)];
    }

    private static async Task<List<string?>> CodesOfAsync(IEnumerable<HttpResponseMessage> responses)
    {
        var codes = new List<string?>();
        foreach (HttpResponseMessage response in responses)
        {
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
            codes.Add(body.TryGetProperty("code", out JsonElement code) ? code.GetString() : null);
        }

        return codes;
    }

    private int BookingsOn(string date)
    {
        DateTime day = DateTime.SpecifyKind(DateTime.Parse(date, CultureInfo.InvariantCulture).Date, DateTimeKind.Utc);
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Bookings.Count(b => !b.IsCancelled && b.Date >= day && b.Date < day.AddDays(1));
    }

    [Fact]
    public async Task SimultaneousAdminCreates_ForOneTable_BookItOnce()
    {
        Floor floor = SeededFloor();
        string date = DayAt(20);
        HttpClient client = _factory.CreateAuthenticatedClient();

        List<HttpResponseMessage> responses = await RaceAsync(Enumerable.Range(0, Racers)
            .Select(_ => (Func<Task<HttpResponseMessage>>)(() => client.PostAsJsonAsync("/api/admin/bookings", AdminBooking(floor, floor.FourTop, date)))));

        Assert.Single(responses, r => r.IsSuccessStatusCode);
        List<HttpResponseMessage> refused = responses.Where(r => !r.IsSuccessStatusCode).ToList();
        Assert.All(refused, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.All(await CodesOfAsync(refused), code => Assert.Equal(ErrorCodes.BookingTableConflict, code));
        Assert.Equal(1, BookingsOn(date));
    }

    [Fact]
    public async Task SimultaneousGuestCreates_ForOneTable_BookItOnce()
    {
        Floor floor = SeededFloor();
        string date = DayAt(21);
        HttpClient client = _factory.CreateClient();

        List<HttpResponseMessage> responses = await RaceAsync(Enumerable.Range(0, Racers)
            .Select(_ => (Func<Task<HttpResponseMessage>>)(() => client.PostAsJsonAsync("/api/bookings",
                GuestBooking(floor.RestaurantId, floor.SectionId, floor.FourTop, date)))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        List<HttpResponseMessage> refused = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
        Assert.All(refused, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.All(await CodesOfAsync(refused), code => Assert.Equal(ErrorCodes.BookingTableConflict, code));
        Assert.Equal(1, BookingsOn(date));
    }

    [Fact]
    public async Task SimultaneousGroupAndMemberCreates_BookTheFurnitureOnce()
    {
        Floor floor = SeededFloor();
        int groupId = await EnsureGroupAsync(floor);
        string date = DayAt(22);
        HttpClient client = _factory.CreateClient();

        List<HttpResponseMessage> responses = await RaceAsync(Enumerable.Range(0, Racers)
            .Select(i => (Func<Task<HttpResponseMessage>>)(() => client.PostAsJsonAsync("/api/bookings",
                i % 2 == 0
                    ? GuestBooking(floor.RestaurantId, floor.SectionId, tableId: null, date, seats: 5, tableGroupId: groupId)
                    : GuestBooking(floor.RestaurantId, floor.SectionId, floor.FourTop, date)))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        List<HttpResponseMessage> refused = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
        Assert.All(refused, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.All(await CodesOfAsync(refused), code =>
            Assert.Contains(code, new[] { ErrorCodes.BookingTableConflict, ErrorCodes.TableGroupBookingConflict }));
        Assert.Equal(1, BookingsOn(date));
    }

    [Fact]
    public async Task PartiesRacingForTheLastCovers_OnlyOneIsSeated()
    {
        Floor floor = SeededFloor();
        await SetCoverCapAsync(floor.RestaurantId, 4);
        try
        {
            string date = DayAt(23);
            HttpClient client = _factory.CreateClient();

            // Two parties of four on two different tables: furniture never conflicts, covers do.
            List<HttpResponseMessage> responses = await RaceAsync(Enumerable.Range(0, Racers)
                .Select(i => (Func<Task<HttpResponseMessage>>)(() => client.PostAsJsonAsync("/api/bookings",
                    GuestBooking(floor.RestaurantId, floor.SectionId, i % 2 == 0 ? floor.FourTop : floor.TwoTop, date, seats: i % 2 == 0 ? 4 : 2)))));

            List<HttpResponseMessage> created = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
            List<HttpResponseMessage> refused = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
            Assert.All(refused, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
            Assert.Contains(ErrorCodes.BookingPacingFull, await CodesOfAsync(refused));
            Assert.True(await CoversOnAsync(date) <= 4, "More covers were seated than the cap allows.");
            Assert.NotEmpty(created);
        }
        finally
        {
            await SetCoverCapAsync(floor.RestaurantId, null);
        }
    }

    [Fact]
    public async Task AHeldLock_BlocksOnlyItsOwnRestaurant()
    {
        Floor floor = SeededFloor();
        string date = DayAt(24);
        HttpClient client = _factory.CreateAuthenticatedClient();

        await using var holder = new NpgsqlConnection(_factory.ConnectionString);
        await holder.OpenAsync();
        await using NpgsqlTransaction transaction = await holder.BeginTransactionAsync();
        await using (var take = new NpgsqlCommand(
            $"SELECT pg_advisory_xact_lock({PostgresBookingWriteLock.BookingWrites}, {floor.RestaurantId})", holder, transaction))
        {
            await take.ExecuteNonQueryAsync();
        }

        Task<HttpResponseMessage> blocked = client.PostAsJsonAsync("/api/admin/bookings", AdminBooking(floor, floor.FourTop, date));
        await WaitForAnAdvisoryLockWaiterAsync();
        HttpResponseMessage elsewhere = await client.PostAsJsonAsync("/api/admin/bookings", new
        {
            restaurantId = floor.OtherRestaurantId,
            sectionId = floor.OtherSectionId,
            tableId = floor.OtherTable,
            date,
            seats = 2,
            customerEmail = "racer@test.com",
            customerPhone = TestPhones.Valid,
        });

        Assert.True(elsewhere.IsSuccessStatusCode, $"The other restaurant waited on this one's lock: {elsewhere.StatusCode}");
        Assert.False(blocked.IsCompleted, "A booking at the locked restaurant went through while the lock was held.");

        await transaction.CommitAsync();
        HttpResponseMessage released = await blocked;
        Assert.True(released.IsSuccessStatusCode, $"The booking did not go through once the lock was released: {released.StatusCode}");
    }

    private async Task WaitForAnAdvisoryLockWaiterAsync()
    {
        await using var probe = new NpgsqlConnection(_factory.ConnectionString);
        await probe.OpenAsync();
        await using var waiting = new NpgsqlCommand(
            "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND database = (SELECT oid FROM pg_database WHERE datname = current_database())",
            probe);
        DateTime giveUp = DateTime.UtcNow.AddSeconds(15);
        while ((long)(await waiting.ExecuteScalarAsync())! == 0)
        {
            Assert.True(DateTime.UtcNow < giveUp, "The booking never reached the advisory lock.");
            await Task.Delay(50);
        }
    }

    private async Task<int> EnsureGroupAsync(Floor floor)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        TableGroup? existing = await db.TableGroups.FirstOrDefaultAsync(g => g.RestaurantId == floor.RestaurantId);
        if (existing is not null)
        {
            return existing.Id;
        }

        var group = new TableGroup { RestaurantId = floor.RestaurantId, CombinedSeats = 6 };
        group.Members.Add(new TableGroupMembership { TableId = floor.FourTop });
        group.Members.Add(new TableGroupMembership { TableId = floor.TwoTop });
        db.TableGroups.Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    }

    private async Task SetCoverCapAsync(int restaurantId, int? cap)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Restaurant restaurant = await db.Restaurants.SingleAsync(r => r.Id == restaurantId);
        restaurant.MaxCoversPerSlot = cap;
        await db.SaveChangesAsync();
    }

    private async Task<int> CoversOnAsync(string date)
    {
        DateTime day = DateTime.SpecifyKind(DateTime.Parse(date, CultureInfo.InvariantCulture).Date, DateTimeKind.Utc);
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings.Where(b => !b.IsCancelled && b.Date >= day && b.Date < day.AddDays(1)).SumAsync(b => b.Seats);
    }
}
