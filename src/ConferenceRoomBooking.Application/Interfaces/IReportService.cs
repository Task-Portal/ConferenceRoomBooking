using ConferenceRoomBooking.Application.DTOs;

namespace ConferenceRoomBooking.Application.Interfaces;

public interface IReportService
{
    /// <summary>Total and per-room revenue/utilization for the given period.</summary>
    Task<RevenueReportDto> GetRevenueReportAsync(DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    /// <summary>Ranks additional services (projector, Wi-Fi, sound, ...) by how often they are booked and revenue generated.</summary>
    Task<IReadOnlyCollection<ServicePopularityReportLine>> GetServicePopularityReportAsync(CancellationToken cancellationToken = default);
}
