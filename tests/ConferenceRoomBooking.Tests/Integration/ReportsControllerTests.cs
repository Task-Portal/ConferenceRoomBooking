using System.Net;
using System.Net.Http.Json;
using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Tests.Integration;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace ConferenceRoomBooking.Tests.Controllers;

public class ReportsControllerTests : IntegrationTestBase
{
    public ReportsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<Guid> GetRoomIdAsync(string roomName)
    {
        var rooms = await Client.GetFromJsonAsync<List<RoomDto>>("/api/rooms");
        var room = rooms!.Single(r => r.Name == roomName);
        return room.Id;
    }

    private static string BuildRevenueReportUrl(DateTime periodStart, DateTime periodEnd)
    {
        // Matches ReportsController's actual route: [Route("api/reports")] + [HttpGet("revenue")]
        // = "api/reports/revenue". A typo'd/renamed path here silently turns every assertion
        // below into "did we get a 404" instead of testing the report logic at all.
        return QueryHelpers.AddQueryString("/api/reports/revenue", new Dictionary<string, string?>
        {
            ["periodStart"] = periodStart.ToString("O"),
            ["periodEnd"] = periodEnd.ToString("O"),
        });
    }

    [Fact]
    public async Task GetRevenueReport_AggregatesAcrossRooms()
    {
        var roomAId = await GetRoomIdAsync("Зал А"); // base rate 2000/hr
        var roomBId = await GetRoomIdAsync("Зал B"); // base rate 3500/hr

        var requestA = new CreateBookingRequest
        {
            RoomId = roomAId,
            StartTime = new DateTime(2027, 7, 2, 10, 0, 0),
            EndTime = new DateTime(2027, 7, 2, 11, 0, 0),
            SelectedServices = new List<string> { "Проєктор" }
        };
        var responseA = await Client.PostAsJsonAsync("api/bookings", requestA);
        Assert.Equal(HttpStatusCode.Created, responseA.StatusCode);
        var singleBooking = await responseA.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(singleBooking);
        Assert.Equal(2000m, singleBooking.RoomRentalCost);
        Assert.Equal(500m, singleBooking.ServicesCost);

        var requestB = new CreateBookingRequest
        {
            RoomId = roomBId,
            StartTime = new DateTime(2027, 7, 2, 12, 0, 0),
            EndTime = new DateTime(2027, 7, 2, 13, 0, 0)
        };
        var responseB = await Client.PostAsJsonAsync("api/bookings", requestB);
        Assert.Equal(HttpStatusCode.Created, responseB.StatusCode);
        var resultResponseB = await responseB.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(resultResponseB);
        Assert.Equal(4025m, resultResponseB.RoomRentalCost);
        Assert.Equal(0m, resultResponseB.ServicesCost);

        var url = BuildRevenueReportUrl(new DateTime(2027, 7, 2, 8, 0, 0), new DateTime(2027, 7, 2, 20, 0, 0));
        var responseReport = await Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, responseReport.StatusCode);

        var resultReport = await responseReport.Content.ReadFromJsonAsync<RevenueReportDto>();
        Assert.NotNull(resultReport);

        Assert.Equal(2, resultReport.TotalBookings);
        Assert.Equal(6025m, resultReport.TotalRoomRentalRevenue);
        Assert.Equal(500m, resultReport.TotalServicesRevenue);
        Assert.Equal(6525m, resultReport.TotalRevenue);
        Assert.Equal(2500m, resultReport.RoomBreakdown.Single(r => r.RoomName == "Зал А").TotalRevenue); // 2000 (оренда) + 500 (Проєктор)
        Assert.Equal(4025m, resultReport.RoomBreakdown.Single(r => r.RoomName == "Зал B").TotalRevenue); // 4025 (оренда) + 0 (без послуг)
    }

    [Fact]
    public async Task GetRevenueReport_ExcludesCancelledBookings()
    {
        var roomId = await GetRoomIdAsync("Зал C");

        var request = new CreateBookingRequest
        {
            RoomId = roomId,
            StartTime = new DateTime(2027, 6, 2, 12, 0, 0),
            EndTime = new DateTime(2027, 6, 2, 13, 0, 0)
        };

        var response = await Client.PostAsJsonAsync("api/bookings", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdBooking = await response.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(createdBooking);

        var cancelResponse = await Client.PostAsync($"api/bookings/{createdBooking.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var url = BuildRevenueReportUrl(new DateTime(2027, 6, 2, 12, 0, 0), new DateTime(2027, 6, 2, 18, 0, 0));
        var responseReport = await Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, responseReport.StatusCode);

        var resultReport = await responseReport.Content.ReadFromJsonAsync<RevenueReportDto>();
        Assert.NotNull(resultReport);
        Assert.Equal(0, resultReport.TotalBookings);
        Assert.Equal(0m, resultReport.TotalRevenue);
    }

    [Fact]
    public async Task GetServicePopularityReport_RanksMostBookedServiceFirst()
    {
        var roomAId = await GetRoomIdAsync("Зал А");

        var requestA = new CreateBookingRequest
        {
            RoomId = roomAId,
            StartTime = new DateTime(2027, 7, 2, 8, 0, 0),
            EndTime = new DateTime(2027, 7, 2, 9, 0, 0),
            SelectedServices = new List<string> { "Wi-Fi" }
        };

        var requestB = new CreateBookingRequest
        {
            RoomId = roomAId,
            StartTime = new DateTime(2027, 7, 2, 9, 0, 0),
            EndTime = new DateTime(2027, 7, 2, 10, 0, 0),
            SelectedServices = new List<string> { "Wi-Fi" }
        };

        var requestC = new CreateBookingRequest
        {
            RoomId = roomAId,
            StartTime = new DateTime(2027, 7, 2, 10, 0, 0),
            EndTime = new DateTime(2027, 7, 2, 11, 0, 0),
            SelectedServices = new List<string> { "Проєктор" }
        };

        var responseA = await Client.PostAsJsonAsync("api/bookings", requestA);
        Assert.Equal(HttpStatusCode.Created, responseA.StatusCode);

        var responseB = await Client.PostAsJsonAsync("api/bookings", requestB);
        Assert.Equal(HttpStatusCode.Created, responseB.StatusCode);

        var responseC = await Client.PostAsJsonAsync("api/bookings", requestC);
        Assert.Equal(HttpStatusCode.Created, responseC.StatusCode);

        var popularityResponse = await Client.GetAsync("api/reports/service-popularity");
        Assert.Equal(HttpStatusCode.OK, popularityResponse.StatusCode);

        // The endpoint returns a JSON array (IReadOnlyCollection<ServicePopularityReportLine>),
        // so we deserialize into a List<T>, not a single T - same pattern as List<RoomDto> above.
        var services = await popularityResponse.Content.ReadFromJsonAsync<List<ServicePopularityReportLine>>();
        Assert.NotNull(services);
        Assert.NotEmpty(services);

        // ReportService orders by TimesBooked descending, so Wi-Fi (booked twice) should lead.
        Assert.Equal("Wi-Fi", services[0].ServiceName);
        Assert.Equal(2, services[0].TimesBooked);

        var projectorLine = services.Single(s => s.ServiceName == "Проєктор");
        Assert.Equal(1, projectorLine.TimesBooked);
    }
}
