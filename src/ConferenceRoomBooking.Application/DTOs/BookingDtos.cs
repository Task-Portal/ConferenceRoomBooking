using System.ComponentModel.DataAnnotations;

namespace ConferenceRoomBooking.Application.DTOs;

public sealed class CreateBookingRequest
{
    [Required]
    public Guid RoomId { get; init; }

    [Required]
    public DateTime StartTime { get; init; }

    [Required]
    public DateTime EndTime { get; init; }

    public List<string> SelectedServices { get; init; } = new();

    [StringLength(200)]
    public string? CustomerName { get; init; }
}

/// <summary>Line-item breakdown of how the final price was calculated - useful for transparency to the client.</summary>
public sealed record PriceBreakdownLineDto(string SegmentDescription, TimeSpan Duration, decimal Rate, decimal Amount);

public sealed record BookingDto(
    Guid Id,
    Guid RoomId,
    string RoomName,
    DateTime StartTime,
    DateTime EndTime,
    IReadOnlyCollection<string> SelectedServices,
    decimal ServicesCost,
    decimal RoomRentalCost,
    decimal TotalPrice,
    string? CustomerName,
    string Status,
    IReadOnlyCollection<PriceBreakdownLineDto> Breakdown);
