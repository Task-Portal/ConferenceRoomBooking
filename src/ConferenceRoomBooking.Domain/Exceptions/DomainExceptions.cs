namespace ConferenceRoomBooking.Domain.Exceptions;

/// <summary>Base type for all predictable, "business rule" failures. Mapped to HTTP 4xx by the API layer.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class RoomNotFoundException : DomainException
{
    public RoomNotFoundException(Guid roomId) : base($"Room with id '{roomId}' was not found.") { }
}

public sealed class BookingNotFoundException : DomainException
{
    public BookingNotFoundException(Guid bookingId) : base($"Booking with id '{bookingId}' was not found.") { }
}

/// <summary>Thrown when a room is requested for a time slot that is already booked.</summary>
public sealed class RoomNotAvailableException : DomainException
{
    public RoomNotAvailableException(Guid roomId, DateTime start, DateTime end)
        : base($"Room '{roomId}' is not available between {start:g} and {end:g}.") { }
}

public sealed class InvalidBookingRequestException : DomainException
{
    public InvalidBookingRequestException(string message) : base(message) { }
}
