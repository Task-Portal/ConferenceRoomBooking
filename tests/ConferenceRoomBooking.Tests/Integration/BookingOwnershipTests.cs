using System.Net;
using System.Net.Http.Json;
using ConferenceRoomBooking.Application.DTOs;
using Xunit;

namespace ConferenceRoomBooking.Tests.Integration;

public class BookingOwnershipTests : IntegrationTestBase
{
    public BookingOwnershipTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CancelBooking_OwnBooking_Succeeds()
    {
    
        AuthenticateAs(await RegisterNewCustomerAndGetTokenAsync());
        
        var roomId = await GetRoomIdAsync("Зал C");
        var request = new CreateBookingRequest
        {
            RoomId = roomId,
            StartTime = new DateTime(2030, 6, 3, 9, 0, 0),
            EndTime = new DateTime(2030, 6, 3, 10, 0, 0)
        };
        var response = await Client.PostAsJsonAsync("api/bookings", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdBooking = await response.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(createdBooking);

    
        
        var cancelResponse = await Client.PostAsync($"/api/bookings/{createdBooking.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_SomeoneElsesBooking_ReturnsForbidden()
    {
    

        AuthenticateAs(await RegisterNewCustomerAndGetTokenAsync());
        var roomId = await GetRoomIdAsync("Зал C");
        var request = new CreateBookingRequest
        {
            RoomId = roomId,
            StartTime = new DateTime(2030, 6, 3, 9, 0, 0),
            EndTime = new DateTime(2030, 6, 3, 10, 0, 0)
        };
        var response = await Client.PostAsJsonAsync("api/bookings", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdBooking = await response.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(createdBooking);

        AuthenticateAs(await RegisterNewCustomerAndGetTokenAsync());

        
        var cancelResponse = await Client.PostAsync($"/api/bookings/{createdBooking.Id}/cancel", null);
        
        Assert.Equal(HttpStatusCode.Forbidden, cancelResponse.StatusCode);

        var secResponse = await cancelResponse.Content.ReadFromJsonAsync<ProblemResponse>(CaseInsensitiveJson);
        
        Assert.NotNull(secResponse);
        Assert.Equal("Forbidden", secResponse.Title);

        AuthenticateAs(await LoginAsAdminAndGetTokenAsync());
        var requestBooking = await Client.GetAsync($"/api/bookings/{createdBooking.Id}");
        var result = await requestBooking.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(result);
        Assert.Equal("Confirmed", result.Status);
    }

    [Fact]
    public async Task CancelBooking_AsAdmin_CanCancelAnyonesBooking()
    {
        
        AuthenticateAs(await RegisterNewCustomerAndGetTokenAsync());
        var roomId = await GetRoomIdAsync("Зал C");
        var request = new CreateBookingRequest
        {
            RoomId = roomId,
            StartTime = new DateTime(2030, 6, 3, 9, 0, 0),
            EndTime = new DateTime(2030, 6, 3, 10, 0, 0)
        };
        var response = await Client.PostAsJsonAsync("api/bookings", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdBooking = await response.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(createdBooking);
        
        
        AuthenticateAs(await LoginAsAdminAndGetTokenAsync());
        
    
        var cancelResponse = await Client.PostAsync($"/api/bookings/{createdBooking.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);
        
    }
}