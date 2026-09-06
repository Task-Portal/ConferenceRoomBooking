using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Application.Interfaces;
using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Domain.Interfaces;

namespace ConferenceRoomBooking.Application.Services;

/// <summary>
/// Produces business-facing analytics on top of the same repositories used for booking.
/// Kept separate from BookingService/RoomService (Single Responsibility) since reporting
/// concerns (aggregation, date ranges) evolve independently from the booking workflow.
/// </summary>
public sealed class ReportService : IReportService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPricingCalculator _pricingCalculator;

    public ReportService(IRoomRepository roomRepository, IBookingRepository bookingRepository, IPricingCalculator pricingCalculator)
    {
        _roomRepository = roomRepository;
        _bookingRepository = bookingRepository;
        _pricingCalculator = pricingCalculator;
    }

    public async Task<RevenueReportDto> GetRevenueReportAsync(DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        if (periodEnd <= periodStart)
        {
            throw new ArgumentException("Report period end must be after period start.");
        }

        var rooms = await _roomRepository.GetAllAsync(cancellationToken);
        var allBookings = await _bookingRepository.GetAllAsync(cancellationToken);

        var relevantBookings = allBookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.StartTime < periodEnd && b.EndTime > periodStart)
            .ToList();

        var lines = new List<RoomUtilizationReportLine>();
        decimal totalRoomRevenue = 0m;
        decimal totalServicesRevenue = 0m;

        var periodHours = (decimal)(periodEnd - periodStart).TotalHours;

        foreach (var room in rooms)
        {
            var roomBookings = relevantBookings.Where(b => b.RoomId == room.Id).ToList();

            decimal roomRevenue = 0m;
            decimal servicesRevenue = 0m;
            decimal bookedHours = 0m;

            foreach (var booking in roomBookings)
            {
                var services = room.ResolveServices(booking.SelectedServiceNames);
                var pricing = _pricingCalculator.Calculate(room, booking.StartTime, booking.EndTime, services);

                roomRevenue += pricing.RoomRentalCost;
                servicesRevenue += pricing.ServicesCost;
                bookedHours += (decimal)booking.Duration.TotalHours;
            }

            totalRoomRevenue += roomRevenue;
            totalServicesRevenue += servicesRevenue;

            var occupancy = periodHours > 0 ? Math.Round(bookedHours / periodHours * 100m, 2) : 0m;

            lines.Add(new RoomUtilizationReportLine(
                room.Id,
                room.Name,
                roomBookings.Count,
                roomRevenue + servicesRevenue,
                Math.Round(bookedHours, 2),
                occupancy));
        }

        return new RevenueReportDto(
            periodStart,
            periodEnd,
            relevantBookings.Count,
            totalRoomRevenue + totalServicesRevenue,
            totalServicesRevenue,
            totalRoomRevenue,
            lines.OrderByDescending(l => l.TotalRevenue).ToList());
    }

    public async Task<IReadOnlyCollection<ServicePopularityReportLine>> GetServicePopularityReportAsync(CancellationToken cancellationToken = default)
    {
        var rooms = await _roomRepository.GetAllAsync(cancellationToken);
        var roomsById = rooms.ToDictionary(r => r.Id);

        var allBookings = await _bookingRepository.GetAllAsync(cancellationToken);
        var confirmedBookings = allBookings.Where(b => b.Status == BookingStatus.Confirmed).ToList();

        var stats = new Dictionary<string, (int Count, decimal Revenue)>(StringComparer.OrdinalIgnoreCase);

        foreach (var booking in confirmedBookings)
        {
            if (!roomsById.TryGetValue(booking.RoomId, out var room))
            {
                continue;
            }

            foreach (var serviceName in booking.SelectedServiceNames)
            {
                var service = room.Services.FirstOrDefault(s => string.Equals(s.Name, serviceName, StringComparison.OrdinalIgnoreCase));
                if (service is null)
                {
                    continue;
                }

                var current = stats.TryGetValue(service.Name, out var value) ? value : (0, 0m);
                stats[service.Name] = (current.Item1 + 1, current.Item2 + service.Price);
            }
        }

        return stats
            .Select(kv => new ServicePopularityReportLine(kv.Key, kv.Value.Count, kv.Value.Revenue))
            .OrderByDescending(l => l.TimesBooked)
            .ToList();
    }
}
