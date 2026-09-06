using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Domain.Entities;

namespace ConferenceRoomBooking.Application.Interfaces;

public sealed record PricingResult(
    decimal RoomRentalCost,
    decimal ServicesCost,
    decimal TotalPrice,
    IReadOnlyCollection<PriceBreakdownLineDto> Breakdown);

/// <summary>
/// Encapsulates the "how much does this booking cost" business rule in one place
/// (Single Responsibility Principle), so pricing changes never require touching
/// controllers or repositories.
/// </summary>
public interface IPricingCalculator
{
    PricingResult Calculate(Room room, DateTime startTime, DateTime endTime, IReadOnlyCollection<Service> selectedServices);
}
