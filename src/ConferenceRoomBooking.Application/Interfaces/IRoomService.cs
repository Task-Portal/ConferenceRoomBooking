using ConferenceRoomBooking.Application.DTOs;

namespace ConferenceRoomBooking.Application.Interfaces;

public interface IRoomService
{
    Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken cancellationToken = default);

    Task<RoomDto> UpdateRoomAsync(Guid roomId, UpdateRoomRequest request, CancellationToken cancellationToken = default);

    Task DeleteRoomAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<RoomDto> GetRoomAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RoomDto>> GetAllRoomsAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds rooms that fit the required capacity and have no overlapping booking in the requested window.</summary>
    Task<IReadOnlyCollection<RoomDto>> FindAvailableRoomsAsync(RoomAvailabilityRequest request, CancellationToken cancellationToken = default);
}
