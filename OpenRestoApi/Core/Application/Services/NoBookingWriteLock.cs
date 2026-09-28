using OpenRestoApi.Core.Application.Interfaces;

namespace OpenRestoApi.Core.Application.Services;

/// <summary>
/// Runs the write with no lock at all: the fallback for a service constructed by hand in a unit
/// test, where nothing runs concurrently. The app registers the PostgreSQL lock.
/// </summary>
/// <seealso>BookingWriteLockTests.TheAppRegistersThePostgresLock</seealso>
public sealed class NoBookingWriteLock : IBookingWriteLock
{
    public static readonly IBookingWriteLock Instance = new NoBookingWriteLock();

    private NoBookingWriteLock() { }

    public Task<T> RunAsync<T>(int restaurantId, Func<Task<T>> write) => write();
}
