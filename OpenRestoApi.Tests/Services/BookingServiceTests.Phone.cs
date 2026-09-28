using Microsoft.EntityFrameworkCore;
using OpenRestoApi.Core.Application.DTOs;
using OpenRestoApi.Core.Application.Exceptions;
using OpenRestoApi.Core.Application.Interfaces;
using OpenRestoApi.Core.Application.Utilities;
using OpenRestoApi.Core.Domain;
using OpenRestoApi.Infrastructure.Persistence;
using OpenRestoApi.Tests.TestInfrastructure;

namespace OpenRestoApi.Tests.Services;

public partial class BookingServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CreateBookingAsync_RejectsABookingWithNoPhone(string? phone)
    {
        using AppDbContext db = TestDbFactory.Create(nameof(CreateBookingAsync_RejectsABookingWithNoPhone) + phone);
        TestSeed.BasicRestaurant(db);

        ValidationException ex = await Assert.ThrowsAsync<ValidationException>(() => CreateService(db).CreateBookingAsync(new BookingDto
        {
            RestaurantId = 1, SectionId = 1, TableId = 1, CustomerEmail = "guest@example.com",
            CustomerPhone = phone, Seats = 2, Date = DateTime.UtcNow.AddDays(7),
        }));

        Assert.Equal(ErrorCodes.BookingPhoneRequired, ex.Code);
        Assert.Empty(db.Bookings);
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsAPhoneThatIsNotE164()
    {
        using AppDbContext db = TestDbFactory.Create(nameof(CreateBookingAsync_RejectsAPhoneThatIsNotE164));
        TestSeed.BasicRestaurant(db);

        ValidationException ex = await Assert.ThrowsAsync<ValidationException>(() => CreateService(db).CreateBookingAsync(new BookingDto
        {
            RestaurantId = 1, SectionId = 1, TableId = 1, CustomerEmail = "guest@example.com",
            CustomerPhone = "0991234567", Seats = 2, Date = DateTime.UtcNow.AddDays(7),
        }));

        Assert.Equal(ErrorCodes.BookingPhoneInvalid, ex.Code);
        Assert.Empty(db.Bookings);
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsAMissingPhone_BeforeConsumingTheHold()
    {
        using AppDbContext db = TestDbFactory.Create(nameof(CreateBookingAsync_RejectsAMissingPhone_BeforeConsumingTheHold));
        TestSeed.BasicRestaurant(db);
        IHoldService holdService = NewHoldService();
        DateTime date = DateTime.UtcNow.AddDays(7);
        HoldResult? hold = holdService.PlaceHold(1, 1, 1, date);
        Assert.NotNull(hold);

        await Assert.ThrowsAsync<ValidationException>(() => CreateService(db, holdService).CreateBookingAsync(new BookingDto
        {
            RestaurantId = 1, SectionId = 1, TableId = 1, Seats = 2, Date = date, HoldId = hold!.HoldId,
        }));

        Assert.NotNull(holdService.GetHold(hold.HoldId));
    }

    [Fact]
    public async Task CreateBookingAsync_StoresTheNormalizedPhone()
    {
        using AppDbContext db = TestDbFactory.Create(nameof(CreateBookingAsync_StoresTheNormalizedPhone));
        TestSeed.BasicRestaurant(db);

        BookingDto result = await CreateService(db).CreateBookingAsync(new BookingDto
        {
            RestaurantId = 1, SectionId = 1, TableId = 1, CustomerEmail = "guest@example.com",
            CustomerPhone = "+593 (99) 123-4567", Seats = 2, Date = DateTime.UtcNow.AddDays(7),
        });

        Assert.Equal("+593991234567", result.CustomerPhone);
        Assert.Equal("+593991234567", (await db.Bookings.AsNoTracking().SingleAsync()).CustomerPhone);
    }

    [Fact]
    public async Task CreateBookingAsync_GroupBooking_RejectsAMissingPhone()
    {
        using AppDbContext db = TestDbFactory.Create(nameof(CreateBookingAsync_GroupBooking_RejectsAMissingPhone));
        SeedRestaurantWithGroup(db);

        ValidationException ex = await Assert.ThrowsAsync<ValidationException>(() => CreateService(db).CreateBookingAsync(new BookingDto
        {
            RestaurantId = 1, TableGroupId = 1, CustomerEmail = "group@example.com", Seats = 6, Date = DateTime.UtcNow.AddDays(7),
        }));

        Assert.Equal(ErrorCodes.BookingPhoneRequired, ex.Code);
        Assert.Empty(db.Bookings);
    }

    [Fact]
    public async Task CreateBookingAsync_GroupBooking_StoresTheNormalizedPhone()
    {
        using AppDbContext db = TestDbFactory.Create(nameof(CreateBookingAsync_GroupBooking_StoresTheNormalizedPhone));
        SeedRestaurantWithGroup(db);

        BookingDto result = await CreateService(db).CreateBookingAsync(new BookingDto
        {
            RestaurantId = 1, TableGroupId = 1, CustomerEmail = "group@example.com",
            CustomerPhone = "+44 20 7946 0958", Seats = 6, Date = DateTime.UtcNow.AddDays(7),
        });

        Assert.Equal(1, result.TableGroupId);
        Booking stored = await db.Bookings.AsNoTracking().SingleAsync();
        Assert.Equal(1, stored.TableGroupId);
        Assert.Equal("+442079460958", stored.CustomerPhone);
    }

    [Fact]
    public async Task CreateBookingAsync_AutoAssign_StoresThePhone()
    {
        using AppDbContext db = TestDbFactory.Create(nameof(CreateBookingAsync_AutoAssign_StoresThePhone));
        SeedMultiTableRestaurant(db);

        BookingDto result = await CreateService(db).CreateBookingAsync(new BookingDto
        {
            RestaurantId = 1, CustomerEmail = "auto@example.com", CustomerPhone = TestPhones.Valid,
            Seats = 2, Date = DateTime.UtcNow.AddDays(7),
        });

        Assert.NotNull(result.TableId);
        Assert.Equal(TestPhones.Valid, (await db.Bookings.AsNoTracking().SingleAsync()).CustomerPhone);
    }

    [Fact]
    public async Task GetBookingByRefAsync_ReadsABookingTakenBeforePhonesWereCollected()
    {
        using AppDbContext db = TestDbFactory.Create(nameof(GetBookingByRefAsync_ReadsABookingTakenBeforePhonesWereCollected));
        TestSeed.BasicRestaurant(db);
        DateTime date = DateTime.UtcNow.AddDays(3);
        db.Bookings.Add(new Booking
        {
            RestaurantId = 1, SectionId = 1, TableId = 1, Seats = 2, Date = date, EndTime = date.AddHours(1),
            BookingRef = "LEGACY", CustomerEmail = "old@example.com",
        });
        db.SaveChanges();

        BookingDto? result = await CreateService(db).GetBookingByRefAsync("LEGACY");

        Assert.NotNull(result);
        Assert.Null(result!.CustomerPhone);
    }
}
