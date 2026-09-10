using System.Net.Http.Json;
using System.Text.Json;
using ConferenceRoomBooking.Application.DTOs;
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

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}