using System.Collections.Concurrent;
using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Domain.Interfaces;

namespace ConferenceRoomBooking.Infrastructure.Repositories;

/// <summary>Thread-safe in-memory implementation of <see cref="IBookingRepository"/>. See <see cref="InMemoryRoomRepository"/> for rationale.</summary>
public sealed class InMemoryBookingRepository : IBookingRepository
{
    private readonly ConcurrentDictionary<Guid, Booking> _bookings = new();

    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _bookings.TryGetValue(id, out var booking);
        return Task.FromResult(booking);
    }

    public Task<IReadOnlyCollection<Booking>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Booking> result = _bookings.Values.ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyCollection<Booking>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Booking> result = _bookings.Values.Where(b => b.RoomId == roomId).ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        if (!_bookings.TryAdd(booking.Id, booking))
        {
            throw new InvalidOperationException($"Booking with id '{booking.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        _bookings[booking.Id] = booking;
        return Task.CompletedTask;
    }
}
