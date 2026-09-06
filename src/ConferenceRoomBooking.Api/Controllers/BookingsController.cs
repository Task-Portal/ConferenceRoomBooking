using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceRoomBooking.Api.Controllers;

/// <summary>Book conference rooms and manage existing bookings.</summary>
[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    /// <summary>
    /// Books a room for a given time window and set of services, returning the full price breakdown.
    /// Fails with 409 Conflict if the room is already booked for an overlapping period.
    /// </summary>
    /// <response code="201">Booking created; total rental cost included in the response.</response>
    /// <response code="400">Invalid request (e.g. unknown service, bad time range).</response>
    /// <response code="404">Room not found.</response>
    /// <response code="409">Room is already booked for that time window.</response>
    [HttpPost]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> CreateBooking([FromBody] CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await _bookingService.CreateBookingAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
    }

    /// <summary>Gets a single booking, including its price breakdown, by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> GetBooking(Guid id, CancellationToken cancellationToken)
    {
        var booking = await _bookingService.GetBookingAsync(id, cancellationToken);
        return Ok(booking);
    }

    /// <summary>Lists all bookings (confirmed and cancelled).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<BookingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<BookingDto>>> GetAllBookings(CancellationToken cancellationToken)
    {
        var bookings = await _bookingService.GetAllBookingsAsync(cancellationToken);
        return Ok(bookings);
    }

    /// <summary>Cancels an existing booking, freeing up the room for that time slot.</summary>
    /// <response code="204">Booking cancelled.</response>
    /// <response code="404">Booking not found.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBooking(Guid id, CancellationToken cancellationToken)
    {
        await _bookingService.CancelBookingAsync(id, cancellationToken);
        return NoContent();
    }
}
