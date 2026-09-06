namespace ConferenceRoomBooking.Domain.Entities;

public enum BookingStatus
{
    Confirmed = 0,
    Cancelled = 1
}

/// <summary>
/// A confirmed reservation of a room for a specific time slot.
/// The total price is calculated once at booking time and stored,
/// so historical bookings are never affected by later price changes.
/// </summary>
public class Booking
{
    public Guid Id { get; private set; }
    public Guid RoomId { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public IReadOnlyCollection<string> SelectedServiceNames { get; private set; } = Array.Empty<string>();
    
    public decimal TotalPrice { get; private set; }
    public string? CustomerName { get; private set; }
    public BookingStatus Status { get; private set; } = BookingStatus.Confirmed;
    public DateTime CreatedAtUtc { get; private set; }

    private Booking() { }

    public Booking(
        Guid roomId,
        DateTime startTime,
        DateTime endTime,
        IEnumerable<string> selectedServiceNames,
        decimal totalPrice,
        string? customerName = null)
    {
        if (endTime <= startTime)
        {
            throw new ArgumentException("Booking end time must be after start time.");
        }

        if (totalPrice < 0)
        {
            throw new ArgumentException("Total price cannot be negative.", nameof(totalPrice));
        }

        Id = Guid.NewGuid();
        RoomId = roomId;
        StartTime = startTime;
        EndTime = endTime;
        SelectedServiceNames = selectedServiceNames.ToArray();
        TotalPrice = totalPrice;
        CustomerName = customerName;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public TimeSpan Duration => EndTime - StartTime;

    public bool OverlapsWith(DateTime otherStart, DateTime otherEnd)
    {
        return Status == BookingStatus.Confirmed && StartTime < otherEnd && otherStart < EndTime;
    }

    public void Cancel() => Status = BookingStatus.Cancelled;
}
