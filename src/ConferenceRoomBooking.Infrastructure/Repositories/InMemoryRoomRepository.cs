using System.Collections.Concurrent;
using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Domain.Interfaces;

namespace ConferenceRoomBooking.Infrastructure.Repositories;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IRoomRepository"/>.
/// Registered as a singleton so data survives across requests within the process.
/// Swap this for an EF Core-backed repository (same interface) when a real database is introduced -
/// nothing in the Application or Api layers needs to change.
/// </summary>
public sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly ConcurrentDictionary<Guid, Room> _rooms = new();

    public Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _rooms.TryGetValue(id, out var room);
        return Task.FromResult(room is { IsDeleted: false } ? room : null);
    }

    public Task<IReadOnlyCollection<Room>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Room> result = _rooms.Values.Where(r => !r.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(Room room, CancellationToken cancellationToken = default)
    {
        if (!_rooms.TryAdd(room.Id, room))
        {
            throw new InvalidOperationException($"Room with id '{room.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Room room, CancellationToken cancellationToken = default)
    {
        _rooms[room.Id] = room;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_rooms.TryGetValue(id, out var room))
        {
            room.MarkDeleted();
        }

        return Task.CompletedTask;
    }
}
