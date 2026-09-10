using System.Net;
using System.Net.Http.Json;
using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Tests.Integration;
using Xunit;

namespace ConferenceRoomBooking.Tests.Integration;

public class BookingConcurrencyTests : IntegrationTestBase
{
    public BookingConcurrencyTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<Guid> GetRoomIdAsync(string roomName)
    {
        var rooms = await Client.GetFromJsonAsync<List<RoomDto>>("/api/rooms");
        var room = rooms!.Single(r => r.Name == roomName);
        return room.Id;
    }

    [Fact]
    public async Task CreateBooking_ManyConcurrentRequestsForSameSlot_OnlyOneSucceeds()
    {
        var roomId = await GetRoomIdAsync("Зал А");
        var request = new CreateBookingRequest
        {
            RoomId = roomId,
            StartTime = new DateTime(2030, 1, 1, 10, 0, 0),
            EndTime = new DateTime(2030, 1, 1, 11, 0, 0)
        };

        const int concurrentRequests = 10;

        // Fire all requests without awaiting individually - .ToArray() forces the lazy
        // Select to actually run, kicking off every PostAsJsonAsync call before any of
        // them has a chance to complete.
        var allRequests = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Client.PostAsJsonAsync("api/bookings", request))
            .ToArray();

        // await Task.WhenAll (not Task.WaitAll!) - stays fully async, and hands back the
        // completed HttpResponseMessage[] directly, so no .Result is needed anywhere below.
        var responses = await Task.WhenAll(allRequests);

        var countConflict = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        var countCreated = responses.Count(r => r.StatusCode == HttpStatusCode.Created);

        Assert.Equal(1, countCreated);
        Assert.Equal(concurrentRequests - 1, countConflict);
    }
}