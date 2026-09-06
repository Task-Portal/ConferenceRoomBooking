using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Application.Interfaces;
using ConferenceRoomBooking.Domain.Entities;

namespace ConferenceRoomBooking.Application.Services;

/// <summary>
/// Implements the rental pricing rules from the specification:
///   - 06:00-09:00 (morning)   -> 10% discount
///   - 09:00-18:00 (standard)  -> base rate
///   - 12:00-14:00 (peak)      -> 15% surcharge (overrides the standard rate during this window)
///   - 18:00-23:00 (evening)   -> 20% discount
///   - 23:00-06:00 (undefined by the spec) -> treated as the base rate
///
/// A booking can span any combination of these bands (e.g. 08:00-15:00 touches morning,
/// standard and peak), so the room's hourly rate is applied per minute against whichever
/// band that minute falls into, then summed. This keeps the calculation exact regardless
/// of how a booking is sliced, and keeps this single class the only place pricing rules live.
/// </summary>
public sealed class PricingCalculator : IPricingCalculator
{
    private static readonly IReadOnlyList<(TimeSpan Start, TimeSpan End, decimal Multiplier, string Description)> DayBands = new[]
    {
        (TimeSpan.FromHours(0), TimeSpan.FromHours(6), 1.00m, "Нічні години (базовий тариф)"),
        (TimeSpan.FromHours(6), TimeSpan.FromHours(9), 0.90m, "Ранкові години (знижка 10%)"),
        (TimeSpan.FromHours(9), TimeSpan.FromHours(12), 1.00m, "Стандартні години (базовий тариф)"),
        (TimeSpan.FromHours(12), TimeSpan.FromHours(14), 1.15m, "Пікові години (націнка 15%)"),
        (TimeSpan.FromHours(14), TimeSpan.FromHours(18), 1.00m, "Стандартні години (базовий тариф)"),
        (TimeSpan.FromHours(18), TimeSpan.FromHours(23), 0.80m, "Вечірні години (знижка 20%)"),
        (TimeSpan.FromHours(23), TimeSpan.FromHours(24), 1.00m, "Нічні години (базовий тариф)"),
    };

    public PricingResult Calculate(Room room, DateTime startTime, DateTime endTime, IReadOnlyCollection<Service> selectedServices)
    {
        if (endTime <= startTime)
        {
            throw new ArgumentException("End time must be after start time.");
        }

        var perBandTotals = new Dictionary<string, (TimeSpan Duration, decimal Rate, decimal Amount)>();

        var cursor = startTime;
        while (cursor < endTime)
        {
            var dayStart = cursor.Date;
            foreach (var band in DayBands)
            {
                var bandStart = dayStart + band.Start;
                var bandEnd = dayStart + band.End;

                var segmentStart = Max(cursor, bandStart);
                var segmentEnd = Min(endTime, bandEnd);

                if (segmentStart >= segmentEnd)
                {
                    continue;
                }

                var hours = (decimal)(segmentEnd - segmentStart).TotalHours;
                var effectiveRate = room.BaseHourlyRate * band.Multiplier;
                var amount = Math.Round(hours * effectiveRate, 2, MidpointRounding.AwayFromZero);

                if (perBandTotals.TryGetValue(band.Description, out var existing))
                {
                    perBandTotals[band.Description] = (existing.Duration + (segmentEnd - segmentStart), effectiveRate, existing.Amount + amount);
                }
                else
                {
                    perBandTotals[band.Description] = (segmentEnd - segmentStart, effectiveRate, amount);
                }
            }

            // Move to the start of the next day to continue processing multi-day bookings.
            cursor = dayStart.AddDays(1);
        }

        var breakdown = perBandTotals
            .Select(kv => new PriceBreakdownLineDto(kv.Key, kv.Value.Duration, kv.Value.Rate, kv.Value.Amount))
            .ToList();

        var roomRentalCost = breakdown.Sum(b => b.Amount);
        var servicesCost = selectedServices.Sum(s => s.Price);

        return new PricingResult(roomRentalCost, servicesCost, roomRentalCost + servicesCost, breakdown);
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
