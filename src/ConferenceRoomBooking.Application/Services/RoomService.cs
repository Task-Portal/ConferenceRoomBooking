using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Application.Interfaces;
using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Domain.Exceptions;
using ConferenceRoomBooking.Domain.Interfaces;

namespace ConferenceRoomBooking.Application.Services;

public sealed class RoomService : IRoomService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IBookingRepository _bookingRepository;

    public RoomService(IRoomRepository roomRepository, IBookingRepository bookingRepository)
    {
        _roomRepository = roomRepository;
        _bookingRepository = bookingRepository;
    }

    public async Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken cancellationToken = default)
    {
        var room = new Room(request.Name, request.Capacity, request.BaseHourlyRate);

        foreach (var service in request.Services)
        {
            room.AddOrUpdateService(service.Name, service.Price);
        }

        await _roomRepository.AddAsync(room, cancellationToken);
        return ToDto(room);
    }

    public async Task<RoomDto> UpdateRoomAsync(Guid roomId, UpdateRoomRequest request, CancellationToken cancellationToken = default)
    {
        var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken)
                    ?? throw new RoomNotFoundException(roomId);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            room.Rename(request.Name);
        }

        if (request.Capacity.HasValue)
        {
            room.SetCapacity(request.Capacity.Value);
        }

        if (request.BaseHourlyRate.HasValue)
        {
            room.SetBaseHourlyRate(request.BaseHourlyRate.Value);
        }

        if (request.ServicesToAddOrUpdate is not null)
        {
            foreach (var service in request.ServicesToAddOrUpdate)
            {
                room.AddOrUpdateService(service.Name, service.Price);
            }
        }

        if (request.ServiceNamesToRemove is not null)
        {
            foreach (var name in request.ServiceNamesToRemove)
            {
                room.RemoveService(name);
            }
        }

        await _roomRepository.UpdateAsync(room, cancellationToken);
        return ToDto(room);
    }

    public async Task DeleteRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken)
                    ?? throw new RoomNotFoundException(roomId);

        await _roomRepository.DeleteAsync(room.Id, cancellationToken);
    }

    public async Task<RoomDto> GetRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken)
                    ?? throw new RoomNotFoundException(roomId);

        return ToDto(room);
    }

    public async Task<IReadOnlyCollection<RoomDto>> GetAllRoomsAsync(CancellationToken cancellationToken = default)
    {
        var rooms = await _roomRepository.GetAllAsync(cancellationToken);
        return rooms.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyCollection<RoomDto>> FindAvailableRoomsAsync(RoomAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        if (request.EndTime <= request.StartTime)
        {
            throw new InvalidBookingRequestException("Search end time must be after start time.");
        }

        var rooms = await _roomRepository.GetAllAsync(cancellationToken);
        var candidateRooms = rooms.Where(r => r.Capacity >= request.MinCapacity).ToList();

        var available = new List<Room>();
        foreach (var room in candidateRooms)
        {
            var bookings = await _bookingRepository.GetByRoomIdAsync(room.Id, cancellationToken);
            var hasConflict = bookings.Any(b => b.OverlapsWith(request.StartTime, request.EndTime));
            if (!hasConflict)
            {
                available.Add(room);
            }
        }

        return available.Select(ToDto).ToList();
    }

    private static RoomDto ToDto(Room room) => new(
        room.Id,
        room.Name,
        room.Capacity,
        room.BaseHourlyRate,
        room.Services.Select(s => new ServiceDto(s.Name, s.Price)).ToList());
}
