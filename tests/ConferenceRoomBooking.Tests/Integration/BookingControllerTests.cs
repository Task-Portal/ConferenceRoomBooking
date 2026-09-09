using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Tests.Integration;
using Xunit;

namespace ConferenceRoomBooking.Tests.Controllers;

public class BookingsControllerTests : IntegrationTestBase
{
    // Matches the anonymous "problem" object ExceptionHandlingMiddleware writes as JSON
    // (title, status, detail, traceId - all lowercase). PropertyNameCaseInsensitive below
    // means this record's PascalCase properties still bind correctly regardless.
    private sealed record ProblemResponse(string Title, int Status, string Detail, string TraceId);

    private static readonly JsonSerializerOptions CaseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

    public BookingsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>Fetches the seeded room's id by name - RoomId is a Guid generated at seed time, so tests can't hardcode it.</summary>
    private async Task<Guid> GetRoomIdAsync(string roomName)
    {
        var rooms = await Client.GetFromJsonAsync<List<RoomDto>>("/api/rooms");
        var room = rooms!.Single(r => r.Name == roomName);
        return room.Id;
    }

    [Fact]
    public async Task CreateBooking_StandardHours_ReturnsCorrectTotalPrice()
    {
        var roomId = await GetRoomIdAsync("Зал А"); // base rate 2000/hr from DataSeeder

        var request = new CreateBookingRequest
        {
            RoomId = roomId,
            StartTime = new DateTime(2030, 6, 2, 10, 0, 0), // 10:00-11:00 = standard hours, no discount/surcharge
            EndTime = new DateTime(2030, 6, 2, 11, 0, 0),
            SelectedServices = new List<string> { "Проєктор" }, // 500 flat, per DataSeeder
            CustomerName = "ТОВ Тест"
        };

        var response = await Client.PostAsJsonAsync("api/bookings", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var fetchedBooking = await response.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(fetchedBooking);
        Assert.Equal(2000m, fetchedBooking.RoomRentalCost);
        Assert.Equal(500m, fetchedBooking.ServicesCost);
        Assert.Equal(2500m, fetchedBooking.TotalPrice);
    }

    [Fact]
    public async Task CreateBooking_OverlappingTimeSlot_ReturnsConflict()
    {
        var roomId = await GetRoomIdAsync("Зал B");

        var firstRequest = new CreateBookingRequest
        {
            RoomId = roomId,
            StartTime = new DateTime(2030, 6, 2, 14, 0, 0),
            EndTime = new DateTime(2030, 6, 2, 16, 0, 0)
        };

        var response = await Client.PostAsJsonAsync("api/bookings", firstRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var overlappingRequest = new CreateBookingRequest
        {
            RoomId = roomId,
            StartTime = new DateTime(2030, 6, 2, 15, 0, 0), // overlaps 14:00-16:00 by one hour
            EndTime = new DateTime(2030, 6, 2, 17, 0, 0)
        };

        var secResponse = await Client.PostAsJsonAsync("api/bookings", overlappingRequest);
        Assert.Equal(HttpStatusCode.Conflict, secResponse.StatusCode);

        // "Room not available" is the JSON body's `title`, written by ExceptionHandlingMiddleware -
        // it is NOT the HTTP reason phrase (that stays the standard "Conflict" for a 409,
        // ASP.NET Core never rewrites it just because we mapped a custom exception here).
        var problem = await secResponse.Content.ReadFromJsonAsync<ProblemResponse>(CaseInsensitiveJson);
        Assert.NotNull(problem);
        Assert.Equal("Room not available", problem.Title);
    }

    [Fact]
    public async Task CreateBooking_UnknownRoomId_ReturnsNotFound()
    {
        var request = new CreateBookingRequest
        {
            RoomId = Guid.NewGuid(),
            StartTime = new DateTime(2027, 6, 2, 15, 0, 0),
            EndTime = new DateTime(2027, 6, 2, 17, 0, 0)
        };

        var response = await Client.PostAsJsonAsync("api/bookings", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_ServiceNotOfferedByRoom_ReturnsBadRequest()
    {
        // "Зал C" (from DataSeeder) does NOT offer "Звук" - only Проєктор and Wi-Fi.
        // Room.ResolveServices(...) throws InvalidOperationException for an unknown service name,
        // which ExceptionHandlingMiddleware maps to 400 BadRequest with title "Invalid request".
        var request = new CreateBookingRequest
        {
            RoomId = await GetRoomIdAsync("Зал C"),
            StartTime = new DateTime(2027, 7, 2, 15, 0, 0),
            EndTime = new DateTime(2027, 7, 2, 17, 0, 0),
            SelectedServices = new List<string> { "Звук" }
        };

        var response = await Client.PostAsJsonAsync("api/bookings", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(CaseInsensitiveJson);
        Assert.NotNull(problem);
        Assert.Equal("Invalid request", problem.Title);
    }

    [Fact]
    public async Task CancelBooking_ThenRebookSameSlot_Succeeds()
    {
        var roomId = await GetRoomIdAsync("Зал C");
        var request = new CreateBookingRequest
        {
            RoomId = roomId,
            StartTime = new DateTime(2030, 6, 3, 9, 0, 0),
            EndTime = new DateTime(2030, 6, 3, 10, 0, 0)
        };

        var response = await Client.PostAsJsonAsync("api/bookings", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); // fail fast here, not on a confusing downstream assert

        var createdBooking = await response.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(createdBooking);

        // No body needed for /cancel - the endpoint doesn't bind one, so sending `request`
        // again here would be misleading (looks like it matters, but it's silently ignored).
        var cancelResponse = await Client.PostAsync($"/api/bookings/{createdBooking.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        // Proves Booking.OverlapsWith(...) correctly ignores cancelled bookings, end-to-end -
        // there's already a unit test for the same rule in
        // DomainEntityTests.Booking_OverlapsWith_IgnoresCancelledBookings.
        var secondTry = await Client.PostAsJsonAsync("api/bookings", request);
        Assert.Equal(HttpStatusCode.Created, secondTry.StatusCode);
    }
}