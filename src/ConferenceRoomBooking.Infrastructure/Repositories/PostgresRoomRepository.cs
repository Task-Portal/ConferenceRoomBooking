using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ConferenceRoomBooking.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL (EF Core) implementation of <see cref="IRoomRepository"/>.
/// Registered as Scoped in DI (one instance - and one AppDbContext - per HTTP request),
/// since DbContext is not thread-safe and must never be shared across requests.
/// </summary>
public sealed class PostgresRoomRepository : IRoomRepository
{
    private readonly AppDbContext _context;

    public PostgresRoomRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // "Not found" is a normal, expected outcome here - it's the caller's (Application layer's)
        // job to decide what that means (e.g. throw RoomNotFoundException, or treat it as optional).
        // A repository should never throw for "no row matched", only for real infrastructure failures.
        //
        // .Include(Services) is required: Room.Services is a navigation property, and EF only
        // loads navigations you explicitly ask for (no lazy-loading proxies are configured here).
        // Without it, every room would come back with an empty Services collection.
        return await _context.Rooms
            .Include(r => r.Services)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Room>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Rooms
            .Include(r => r.Services)
            .Where(r => !r.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Room room, CancellationToken cancellationToken = default)
    {
        await _context.Rooms.AddAsync(room, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Room room, CancellationToken cancellationToken = default)
    {
        // room came from GetByIdAsync using this same (scoped) context, so it's already tracked -
        // SaveChangesAsync alone is enough to persist the changes made via Room's domain methods.
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (room != null)
        {
            room.MarkDeleted();
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
