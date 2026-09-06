using ConferenceRoomBooking.Domain.Entities;

namespace ConferenceRoomBooking.Domain.Interfaces;

/// <summary>
/// Persistence abstraction for rooms. The domain/application layers depend only on this
/// interface, so the in-memory implementation can be swapped for EF Core/SQL/Mongo later
/// without touching business logic (Dependency Inversion Principle).
/// </summary>
public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns every non-deleted room.</summary>
    Task<IReadOnlyCollection<Room>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Room room, CancellationToken cancellationToken = default);

    /// <summary>Persists changes made to a room that was previously fetched via GetByIdAsync.</summary>
    Task UpdateAsync(Room room, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the room so historical bookings/reports remain intact.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
