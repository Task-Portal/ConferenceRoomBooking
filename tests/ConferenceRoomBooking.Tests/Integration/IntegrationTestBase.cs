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

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = Factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        // Nothing to clean up per-test: the next InitializeAsync() call wipes the DB anyway,
        // and the SQLite connection/host are torn down once by CustomWebApplicationFactory.Dispose().
        return Task.CompletedTask;
    }
}