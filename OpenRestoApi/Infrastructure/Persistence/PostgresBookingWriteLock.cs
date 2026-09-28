using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using OpenRestoApi.Core.Application.Interfaces;

namespace OpenRestoApi.Infrastructure.Persistence;

/// <summary>
/// One transaction per write, opened by taking <c>pg_advisory_xact_lock(BookingWrites,
/// restaurantId)</c>: a second writer at the same restaurant waits at that statement until the
/// first commits, and its checks then read the first one's row. Writers at different restaurants
/// never wait on each other. The lock is released by the commit or rollback, so a crashed request
/// cannot leave it held.
/// <para>
/// It shares the request's scoped <see cref="AppDbContext"/> with the repositories the delegate
/// calls, which is what puts their reads and <c>SaveChanges</c> inside the transaction. The whole
/// unit runs under the context's execution strategy, so a transient failure retries the check
/// and the write together; entities a failed attempt added are detached first, so the retry
/// does not insert them twice.
/// </para>
/// </summary>
/// <seealso>BookingWriteLockConcurrencyTests.SimultaneousAdminCreates_ForOneTable_BookItOnce</seealso>
/// <seealso>BookingWriteLockConcurrencyTests.SimultaneousGuestCreates_ForOneTable_BookItOnce</seealso>
/// <seealso>BookingWriteLockConcurrencyTests.SimultaneousGroupAndMemberCreates_BookTheFurnitureOnce</seealso>
/// <seealso>BookingWriteLockConcurrencyTests.PartiesRacingForTheLastCovers_OnlyOneIsSeated</seealso>
/// <seealso>BookingWriteLockConcurrencyTests.AHeldLock_BlocksOnlyItsOwnRestaurant</seealso>
public sealed class PostgresBookingWriteLock(AppDbContext db) : IBookingWriteLock
{
    /// <summary>
    /// First key of the two-key advisory lock, naming the kind of lock; the restaurant id is the
    /// second. Another kind of lock takes another class so the two never collide.
    /// </summary>
    public const int BookingWrites = 1;

    public async Task<T> RunAsync<T>(int restaurantId, Func<Task<T>> write)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            // Nested: the outer call already holds the lock and owns the transaction.
            return await write();
        }

        HashSet<object> addedBefore = AddedEntities();
        bool isRetry = false;
        IExecutionStrategy strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            if (isRetry)
            {
                DetachAddedSince(addedBefore);
            }

            isRetry = true;
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({BookingWrites}, {restaurantId})");
            T result = await write();
            await transaction.CommitAsync();
            return result;
        });
    }

    private HashSet<object> AddedEntities()
        => db.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToHashSet(ReferenceEqualityComparer.Instance);

    private void DetachAddedSince(HashSet<object> addedBefore)
    {
        foreach (EntityEntry entry in db.ChangeTracker.Entries().ToList())
        {
            if (entry.State == EntityState.Added && !addedBefore.Contains(entry.Entity))
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
