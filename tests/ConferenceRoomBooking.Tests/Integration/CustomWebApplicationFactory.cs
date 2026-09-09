using ConferenceRoomBooking.Domain.Interfaces;
using ConferenceRoomBooking.Infrastructure;
using ConferenceRoomBooking.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConferenceRoomBooking.Tests.Integration;

/// <summary>
/// Boots the real app (all controllers, DI, middleware) in-process, but swaps the
/// production Postgres AppDbContext for a SQLite one backed by a single open
/// in-memory connection that lives for as long as this factory does.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public CustomWebApplicationFactory()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Replace the production Postgres registration with SQLite, backed by the
            // single open connection above (so the in-memory DB survives across DbContext instances).
            var appPostgresDbContext =
                services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (appPostgresDbContext != null)
            {
                services.Remove(appPostgresDbContext);
            }

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            // Build a throwaway provider just to create the schema once, right away.
            // This is fine even though it's a *different* ServiceProvider instance than the
            // one the test server will eventually use - the schema lives inside the SQLite
            // connection itself, not in any particular C# object, so it persists regardless
            // of which provider created the DbContext that issued the CREATE TABLE statements.
            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetService<AppDbContext>();
            dbContext?.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }

    /// <summary>
    /// Wipes and recreates the schema, then re-seeds the starting data. Called before every
    /// single test (see IntegrationTestBase.InitializeAsync) so tests never see leftover
    /// state from a previous test, regardless of execution order.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        // `Services` is inherited from WebApplicationFactory<TEntryPoint> - it's the real
        // service provider for the (lazily-built) test host, no manual builder.Build() needed.
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var roomRepository = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        await DataSeeder.SeedAsync(roomRepository);
    }
}
