using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceRoomBooking.Api.Controllers;

/// <summary>Manage conference rooms: create, update, delete, list, and search for availability.</summary>
[ApiController]
[Route("api/rooms")]
[Produces("application/json")]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    /// <summary>Creates a new conference room.</summary>
    /// <response code="201">Room created successfully.</response>
    /// <response code="400">Validation failed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoomDto>> CreateRoom([FromBody] CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var room = await _roomService.CreateRoomAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, room);
    }

    /// <summary>Gets a single room by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> GetRoom(Guid id, CancellationToken cancellationToken)
    {
        var room = await _roomService.GetRoomAsync(id, cancellationToken);
        return Ok(room);
    }

    /// <summary>Lists every active (non-deleted) room.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<RoomDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<RoomDto>>> GetAllRooms(CancellationToken cancellationToken)
    {
        var rooms = await _roomService.GetAllRoomsAsync(cancellationToken);
        return Ok(rooms);
    }

    /// <summary>
    /// Partially updates a room: rename, change price/capacity, and/or add, update, or remove services.
    /// Only the fields supplied in the body are changed.
    /// </summary>
    /// <response code="200">Room updated successfully.</response>
    /// <response code="404">Room not found.</response>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> UpdateRoom(Guid id, [FromBody] UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        var room = await _roomService.UpdateRoomAsync(id, request, cancellationToken);
        return Ok(room);
    }

    /// <summary>Deletes (soft-deletes) a conference room. Past bookings for the room are preserved for reporting.</summary>
    /// <response code="204">Room deleted successfully.</response>
    /// <response code="404">Room not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRoom(Guid id, CancellationToken cancellationToken)
    {
        await _roomService.DeleteRoomAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Searches for rooms that fit the requested capacity and are free for the given time window.</summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(IReadOnlyCollection<RoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<RoomDto>>> FindAvailableRooms([FromQuery] RoomAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var rooms = await _roomService.FindAvailableRoomsAsync(request, cancellationToken);
        return Ok(rooms);
    }
}
