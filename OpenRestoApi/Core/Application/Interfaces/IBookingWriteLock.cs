namespace OpenRestoApi.Core.Application.Interfaces;

/// <summary>
/// Serialises the writes that decide whether a table, group or slot is still free at one
/// restaurant. A booking write is a read (is the unit taken, are there covers left) followed by
/// an insert or update, and two requests interleaving those steps would both see the unit free
/// and both write. Everything that creates a booking or moves one onto a unit or time runs its
/// check and its write inside <see cref="RunAsync{T}"/>.
/// <para>
/// The delegate may run more than once when the database drops the connection mid-transaction,
/// so it must not have side effects outside the database (email, notifications, releasing a
/// hold); those belong after the call returns.
/// </para>
/// </summary>
public interface IBookingWriteLock
{
    Task<T> RunAsync<T>(int restaurantId, Func<Task<T>> write);
}
