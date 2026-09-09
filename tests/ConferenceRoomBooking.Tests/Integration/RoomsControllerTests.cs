using System.Net;
using System.Net.Http.Json;
using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Tests.Integration;
using Xunit;

namespace ConferenceRoomBooking.Tests.Controllers;

public class RoomsControllerTests : IntegrationTestBase
{
    public RoomsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetAllRooms_ReturnsSeededRooms()
    {
        var response = await Client.GetAsync("api/rooms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rooms = await response.Content.ReadFromJsonAsync<List<RoomDto>>();
        Assert.NotNull(rooms);
        Assert.NotEmpty(rooms);
        Assert.True(rooms.Count == 3);
        Assert.True(rooms.Find(r => r is { Name: "Зал А", Capacity: 50 }) != null);
    }

    [Fact]
    public async Task GetRoom_UnknownId_Returns404()
    {
        // $"..." (string interpolation) actually substitutes Guid.NewGuid() into the URL.
        // Without the leading $, "{...}" is just literal text sent as-is.
        var response = await Client.GetAsync($"api/rooms/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateRoom_ThenGetById_ReturnsTheCreatedRoom()
    {
        var newRoom = new CreateRoomRequest
        {
            Name = "Зал D",
            Capacity = 20,
            BaseHourlyRate = 1200,
            Services = new List<ServiceDto>
            {
                new("Проєктор", 500m),
                new("Wi-Fi", 300m)
            }
        };

        // PostAsJsonAsync serializes `newRoom` to JSON and sets Content-Type for you -
        // PostAsync alone only accepts a ready-made HttpContent, not a plain object.
        var response = await Client.PostAsJsonAsync("/api/rooms", newRoom);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location); // CreatedAtAction should have set this.

        // The POST response body already contains the created room (RoomsController returns
        // it directly), so we don't strictly need a second GET - but following the Location
        // header is exactly what a real API client would do, and it's a good way to double-check
        // CreatedAtAction actually points at a working GET route.
        var getResponse = await Client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetchedRoom = await getResponse.Content.ReadFromJsonAsync<RoomDto>();
        Assert.NotNull(fetchedRoom);
        Assert.Equal("Зал D", fetchedRoom!.Name);
        Assert.Equal(20, fetchedRoom.Capacity);

        // The real point of this test: without .Include(r => r.Services) in
        // PostgresRoomRepository, this collection would come back empty.
        Assert.Equal(2, fetchedRoom.Services.Count);
        Assert.Contains(fetchedRoom.Services, s => s.Name == "Проєктор" && s.Price == 500m);
    }
}
