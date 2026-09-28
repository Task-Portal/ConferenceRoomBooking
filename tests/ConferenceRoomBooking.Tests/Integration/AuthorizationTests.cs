using System.Net;
using Xunit;

namespace ConferenceRoomBooking.Tests.Integration;

/// <summary>
/// Verifies the access rules as a whole, endpoint by endpoint. Deliberately data-driven
/// ([Theory]) so that adding a new protected endpoint later is a single new [InlineData]
/// line - and forgetting to protect a new admin endpoint shows up as a failing row.
/// </summary>
public class AuthorizationTests : IntegrationTestBase
{
    // Any well-formed Guid works for the {id} routes: authentication/authorization run
    // BEFORE the action (and before the "does this id exist" lookup), so we get 401/403
    // without the entity having to exist.
    private const string SomeId = "00000000-0000-0000-0000-000000000001";

    public AuthorizationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("POST", "/api/rooms")]
    [InlineData("PATCH", "/api/rooms/" + SomeId)]
    [InlineData("DELETE", "/api/rooms/" + SomeId)]
    [InlineData("POST", "/api/bookings")]
    [InlineData("POST", "/api/bookings/" + SomeId + "/cancel")]
    [InlineData("GET", "/api/bookings")]
    [InlineData("GET", "/api/bookings/" + SomeId)]
    [InlineData("GET", "/api/reports/revenue")]
    [InlineData("GET", "/api/reports/service-popularity")]
    public async Task ProtectedEndpoint_WithoutToken_Returns401(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/rooms")]
    [InlineData("PATCH", "/api/rooms/" + SomeId)]
    [InlineData("DELETE", "/api/rooms/" + SomeId)]
    [InlineData("GET", "/api/bookings")]
    [InlineData("GET", "/api/bookings/" + SomeId)]
    [InlineData("GET", "/api/reports/revenue")]
    [InlineData("GET", "/api/reports/service-popularity")]
    public async Task AdminOnlyEndpoint_AsCustomer_Returns403(string method, string url)
    {
       
        var token = await RegisterNewCustomerAndGetTokenAsync();
        AuthenticateAs(token);
       

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    }

    [Theory]
    [InlineData("/api/rooms")]
    [InlineData("/api/rooms/" + SomeId)]
    [InlineData("/api/rooms/available?startTime=2030-01-01T10:00:00&endTime=2030-01-01T11:00:00")]
    public async Task PublicEndpoint_WithoutToken_IsNotRejectedAsUnauthorized(string url)
    {
       
        var request = new  HttpRequestMessage(new HttpMethod("GET"), url);
        var response = await Client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MalformedToken_Returns401()
    {
        
        AuthenticateAs("this-is-not-a-jwt");
        var request = new HttpRequestMessage(new HttpMethod("GET"), "/api/bookings");
        var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}