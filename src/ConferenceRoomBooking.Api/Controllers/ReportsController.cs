using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceRoomBooking.Api.Controllers;

/// <summary>Business analytics: revenue, room utilization, and service popularity.</summary>
[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Revenue and occupancy report for a date range: total revenue, split between room rental
    /// and services, plus a per-room breakdown sorted by revenue. Useful for management dashboards.
    /// </summary>
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(RevenueReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RevenueReportDto>> GetRevenueReport(
        [FromQuery] DateTime periodStart,
        [FromQuery] DateTime periodEnd,
        CancellationToken cancellationToken)
    {
        var report = await _reportService.GetRevenueReportAsync(periodStart, periodEnd, cancellationToken);
        return Ok(report);
    }

    /// <summary>Ranks additional services (projector, Wi-Fi, sound, ...) by how often they're booked, to guide pricing/promotion decisions.</summary>
    [HttpGet("service-popularity")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ServicePopularityReportLine>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ServicePopularityReportLine>>> GetServicePopularityReport(CancellationToken cancellationToken)
    {
        var report = await _reportService.GetServicePopularityReportAsync(cancellationToken);
        return Ok(report);
    }
}
