using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Tests.Integration;
using Xunit;

/// <summary>
/// Base class for controller integration tests. Each test class shares one (expensive)
/// WebApplicationFactory + SQLite connection via IClassFixture, but xUnit calls
/// InitializeAsync() before *every single test method* - so the database itself is
/// wiped and re-seeded per test, keeping tests independent of execution order.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected sealed record ProblemResponse(string Title, int Status, string Detail, string TraceId);

    protected static readonly JsonSerializerOptions CaseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = Factory.CreateClient();
    }

    protected async Task<Guid> GetRoomIdAsync(string roomName)
    {
        var rooms = await Client.GetFromJsonAsync<List<RoomDto>>("/api/rooms");
        var room = rooms!.Single(r => r.Name == roomName);
        return room.Id;
    }

    /// <summary>Registers a brand-new Customer account (unique email per call, so parallel/repeated calls in one test never collide) and returns its JWT.</summary>
    protected async Task<string> RegisterNewCustomerAndGetTokenAsync()
    {
        var registerRequest = new RegisterRequest
            { Email = $"customer_{Guid.NewGuid():N}@test.local", Password = "some passwrod12323333" };


        var result = await Client.PostAsJsonAsync("api/auth/register", registerRequest);
        Assert.True(result.IsSuccessStatusCode);

        var authResponse = await result.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        return authResponse.Token;
    }

    /// <summary>Logs in as the fixed test Admin account (see CustomWebApplicationFactory.AdminEmail/AdminPassword) and returns its JWT.</summary>
    protected async Task<string> LoginAsAdminAndGetTokenAsync()
    {
        var loginRequest = new LoginRequest
            { Email = CustomWebApplicationFactory.AdminEmail, Password = CustomWebApplicationFactory.AdminPassword };
        var result = await Client.PostAsJsonAsync("api/auth/login", loginRequest);
        Assert.True(result.IsSuccessStatusCode);
        var authResponse = await result.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        return authResponse.Token;
    }

    /// <summary>Attaches a bearer token to every subsequent request this test's Client makes.</summary>
    protected void AuthenticateAs(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}