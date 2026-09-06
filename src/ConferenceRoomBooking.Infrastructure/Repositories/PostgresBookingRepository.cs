using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ConferenceRoomBooking.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL (EF Core) implementation of <see cref="IBookingRepository"/>. See
/// <see cref="PostgresRoomRepository"/> for why "not found" returns null instead of throwing.
/// </summary>
public sealed class PostgresBookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public PostgresBookingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Booking>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Bookings.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Booking>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings.Where(b => b.RoomId == roomId).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await _context.Bookings.AddAsync(booking, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        // Already tracked by this scoped context (fetched via GetByIdAsync earlier in the same request).
        await _context.SaveChangesAsync(cancellationToken);
    }
}

