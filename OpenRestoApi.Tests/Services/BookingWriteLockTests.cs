using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenRestoApi.Core.Application.DTOs;
using OpenRestoApi.Core.Application.Interfaces;
using OpenRestoApi.Core.Application.Services;
using OpenRestoApi.Core.Domain;
using OpenRestoApi.Extensions;
using OpenRestoApi.Infrastructure.Persistence;
using OpenRestoApi.Tests.Integration;

namespace OpenRestoApi.Tests.Services;

/// <summary>
/// Every path that books a unit or moves a booking onto one runs its write inside
/// <see cref="IBookingWriteLock"/>, keyed by the restaurant. The services come from the app's own
/// registrations with only the lock swapped for a recorder, so a path that skips the lock, saves a
/// booking outside it, or a service that DI builds without the lock at all, fails here. That the
/// real lock serialises is <c>BookingWriteLockConcurrencyTests</c>' job.
/// </summary>
public sealed class BookingWriteLockTests : IDisposable
{
    private const int RestaurantId = 1;
    private static readonly DateTime Tomorrow = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(2).AddHours(12), DateTimeKind.Unspecified);
    private static readonly DateTime TomorrowUtc = DateTime.SpecifyKind(Tomorrow, DateTimeKind.Utc);

    private readonly RecordingBookingWriteLock _lock = new();
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _db;

    public BookingWriteLockTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddProjectDependencies();
        string database = $"write-lock-{Guid.NewGuid():N}";
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(database));
        services.AddSingleton<IBookingWriteLock>(_lock);

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _db.Restaurants.Add(new Restaurant { Id = RestaurantId, Name = "R", Timezone = "UTC", DefaultBookingDurationMinutes = 60 });
        _db.Sections.Add(new Section { Id = 1, Name = "Main", RestaurantId = RestaurantId });
        _db.Tables.Add(new Table { Id = 1, Name = "T1", Seats = 4, SectionId = 1 });
        _db.Tables.Add(new Table { Id = 2, Name = "T2", Seats = 4, SectionId = 1 });
        _db.Tables.Add(new Table { Id = 3, Name = "T3", Seats = 4, SectionId = 1 });
        var group = new TableGroup { Id = 1, RestaurantId = RestaurantId, CombinedSeats = 8 };
        group.Members.Add(new TableGroupMembership { TableGroupId = 1, TableId = 2 });
        group.Members.Add(new TableGroupMembership { TableGroupId = 1, TableId = 3 });
        _db.TableGroups.Add(group);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        _lock.Watch(_db);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    /// <summary>
    /// Runs the write like the real lock and counts every save that touches a booking or a
    /// waitlist entry, split by whether the lock was held at the time.
    /// </summary>
    private sealed class RecordingBookingWriteLock : IBookingWriteLock
    {
        private bool _held;

        public List<int> Restaurants { get; } = [];

        public int SavesInside { get; private set; }

        public int SavesOutside { get; private set; }

        public async Task<T> RunAsync<T>(int restaurantId, Func<Task<T>> write)
        {
            Restaurants.Add(restaurantId);
            _held = true;
            try
            {
                return await write();
            }
            finally
            {
                _held = false;
            }
        }

        public void Watch(AppDbContext db) => db.SavingChanges += (_, _) =>
        {
            bool touchesBookings = db.ChangeTracker.Entries()
                .Any(e => e.Entity is Booking or WaitlistEntry
                    && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
            if (!touchesBookings)
            {
                return;
            }

            if (_held)
            {
                SavesInside++;
            }
            else
            {
                SavesOutside++;
            }
        };

        public void Reset()
        {
            Restaurants.Clear();
            SavesInside = 0;
            SavesOutside = 0;
        }
    }

    private T Resolve<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    private void AssertWroteUnderTheLock()
    {
        Assert.Equal([RestaurantId], _lock.Restaurants);
        Assert.True(_lock.SavesInside > 0, "The booking was never saved inside the lock.");
        Assert.Equal(0, _lock.SavesOutside);
    }

    private static BookingDto Guest(int? tableId, int? sectionId, int? groupId = null, int seats = 2) => new()
    {
        RestaurantId = RestaurantId,
        TableId = tableId,
        SectionId = sectionId,
        TableGroupId = groupId,
        Date = Tomorrow,
        Seats = seats,
        CustomerEmail = "guest@example.com",
        CustomerName = "Guest",
        CustomerPhone = TestPhones.Valid,
    };

    private async Task<Booking> SeedBookingAsync(DateTime start, bool cancelled = false)
    {
        var booking = new Booking
        {
            RestaurantId = RestaurantId,
            SectionId = 1,
            TableId = 1,
            Date = start,
            EndTime = start.AddHours(1),
            Seats = 2,
            BookingRef = "seeded",
            IsCancelled = cancelled,
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        _lock.Reset();
        return booking;
    }

    [Fact]
    public async Task GuestCreate_OnATable_WritesUnderTheLock()
    {
        await Resolve<BookingService>().CreateBookingAsync(Guest(tableId: 1, sectionId: 1));

        AssertWroteUnderTheLock();
    }

    [Fact]
    public async Task GuestCreate_OnAGroup_WritesUnderTheLock()
    {
        await Resolve<BookingService>().CreateBookingAsync(Guest(tableId: null, sectionId: 1, groupId: 1, seats: 6));

        AssertWroteUnderTheLock();
    }

    [Fact]
    public async Task GuestCreate_AutoAssigned_WritesUnderTheLock()
    {
        await Resolve<BookingService>().CreateBookingAsync(Guest(tableId: null, sectionId: null));

        AssertWroteUnderTheLock();
    }

    [Fact]
    public async Task GuestUpdate_WritesUnderTheLock()
    {
        Booking seeded = await SeedBookingAsync(TomorrowUtc);
        BookingDto moved = Guest(tableId: 2, sectionId: 1);
        moved.Id = seeded.Id;
        moved.BookingRef = seeded.BookingRef;
        moved.Date = TomorrowUtc.AddHours(2);

        await Resolve<BookingService>().UpdateBookingAsync(seeded.Id, moved);

        AssertWroteUnderTheLock();
    }

    [Fact]
    public async Task AdminCreate_WritesUnderTheLock()
    {
        await Resolve<AdminService>().CreateBookingAsync(new AdminCreateBookingRequest
        {
            RestaurantId = RestaurantId,
            SectionId = 1,
            TableId = 1,
            Date = Tomorrow,
            Seats = 2,
            CustomerEmail = "guest@example.com",
            CustomerPhone = TestPhones.Valid,
        });

        AssertWroteUnderTheLock();
    }

    [Fact]
    public async Task AdminUpdate_MovingTheBooking_WritesUnderTheLock()
    {
        Booking seeded = await SeedBookingAsync(TomorrowUtc);

        await Resolve<AdminService>().AdminUpdateBookingAsync(seeded.Id, new AdminUpdateBookingRequest
        {
            TableId = 2,
            Date = TomorrowUtc.AddHours(3),
        });

        AssertWroteUnderTheLock();
    }

    [Fact]
    public async Task AdminExtend_WritesUnderTheLock()
    {
        Booking seeded = await SeedBookingAsync(TomorrowUtc);

        await Resolve<AdminService>().ExtendBookingAsync(seeded.Id, 30);

        AssertWroteUnderTheLock();
    }

    [Fact]
    public async Task AdminRestore_WritesUnderTheLock()
    {
        Booking seeded = await SeedBookingAsync(TomorrowUtc, cancelled: true);

        await Resolve<AdminService>().RestoreBookingAsync(seeded.Id);

        AssertWroteUnderTheLock();
    }

    [Fact]
    public async Task AdminExtendAllInProgress_WritesUnderTheLock()
    {
        await SeedBookingAsync(DateTime.UtcNow.AddMinutes(-10));

        List<BookingDetailDto>? extended = await Resolve<AdminService>().ExtendAllActiveBookingsAsync(RestaurantId, 15);

        Assert.Single(extended!);
        AssertWroteUnderTheLock();
    }

    [Fact]
    public async Task WaitlistSeat_WritesUnderTheLock()
    {
        _db.WaitlistEntries.Add(new WaitlistEntry
        {
            Id = 1,
            RestaurantId = RestaurantId,
            Ref = "ticket",
            Number = 1,
            Name = "Ada",
            Seats = 2,
            Status = WaitlistStatus.Waiting,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        _lock.Reset();

        await Resolve<WaitlistService>().SeatAsync(1, new SeatWaitlistEntryRequest());

        AssertWroteUnderTheLock();
    }

    [Fact]
    public void TheAppRegistersThePostgresLock()
    {
        using var factory = new TestWebAppFactory();
        using IServiceScope scope = factory.Services.CreateScope();

        Assert.IsType<PostgresBookingWriteLock>(scope.ServiceProvider.GetRequiredService<IBookingWriteLock>());
    }
}
