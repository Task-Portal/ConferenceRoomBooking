namespace ConferenceRoomBooking.Application.DTOs;

/// <summary>Revenue and utilization summary for a single room over the reporting period.</summary>
public sealed record RoomUtilizationReportLine(
    Guid RoomId,
    string RoomName,
    int TotalBookings,
    decimal TotalRevenue,
    decimal TotalBookedHours,
    decimal OccupancyRatePercent);

/// <summary>Aggregated business report: overall revenue plus a per-room breakdown, for a given date range.</summary>
public sealed record RevenueReportDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int TotalBookings,
    decimal TotalRevenue,
    decimal TotalServicesRevenue,
    decimal TotalRoomRentalRevenue,
    IReadOnlyCollection<RoomUtilizationReportLine> RoomBreakdown);

/// <summary>Shows which paid services are booked most often and how much revenue they generate.</summary>
public sealed record ServicePopularityReportLine(string ServiceName, int TimesBooked, decimal TotalRevenue);
