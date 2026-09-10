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
/// production Postgres AppDbContext for a SQLite one backed by a NAMED, shared-cache
/// in-memory database that lives for as long as this factory does.
///
/// Two things had to be true at once, and getting either wrong breaks it:
///
/// 1. Every concurrent HTTP request gets its own Scoped AppDbContext, so it needs its
///    own SqliteConnection object - Microsoft.Data.Sqlite does not support running
///    commands concurrently on a single shared connection object. That's why we pass a
///    connection STRING to UseSqlite (EF Core opens/closes a fresh connection per
///    DbContext), not a single shared SqliteConnection instance.
///
/// 2. A plain "Data Source=:memory:" database is always private to whichever connection
///    opened it - SQLite ignores Cache=Shared for the unadorned ":memory:" name. To
///    actually SHARE one in-memory database across many independent connections, the
///    database needs an explicit name via URI syntax: "file:&lt;name&gt;?mode=memory&amp;cache=shared".
///    (See https://www.sqlite.org/inmemorydb.html, "Named In-Memory Databases".)
///    The name is randomized per factory instance so that if xUnit ever runs multiple
///    test classes in parallel, each gets its own isolated database instead of secretly
///    sharing one.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    // Destroyed the instant its last connection closes, like any shared-cache :memory:
    // database - this connection's only job is to stay open for the factory's whole
    // lifetime so the database survives between/during requests. Nothing ever queries
    // through it directly.
    private readonly SqliteConnection _keepAliveConnection;

    public CustomWebApplicationFactory()
    {
        var databaseName = $"crb_tests_{Guid.NewGuid():N}";

        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = $"file:{databaseName}?mode=memory&cache=shared",
            // If two connections genuinely try to write at the same moment, SQLite's single-writer
            // lock makes the second one wait rather than fail outright - but only for up to this
            // many seconds before giving up and throwing SQLITE_BUSY. Without this, the default
            // timeout is effectively immediate, and concurrent writers surface as random exceptions
            // instead of just waiting their turn (which is what happens for real against Postgres).
            DefaultTimeout = 30
        };
        _connectionString = connectionStringBuilder.ToString();

        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var appPostgresDbContext =
                services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (appPostgresDbContext != null)
            {
                services.Remove(appPostgresDbContext);
            }

            // Connection STRING (not the keep-alive connection object) - each DbContext now
            // opens/closes its own connection, same lifecycle as it would with Npgsql. The
            // named shared-cache database (see class doc comment) is what makes them all
            // still see the exact same data.
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connectionString));

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetService<AppDbContext>();
            dbContext?.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _keepAliveConnection.Dispose();
    }

    /// <summary>
    /// Wipes the DATA (not the schema) and re-seeds the starting data. Called before every
    /// single test (see IntegrationTestBase.InitializeAsync) so tests never see leftover
    /// state from a previous test, regardless of execution order.
    ///
    /// Deliberately does NOT use Database.EnsureDeletedAsync()/EnsureCreatedAsync() here.
    /// EnsureDeleted's SQLite implementation is built around deleting the database FILE -
    /// for a named, shared-cache :memory: database (kept alive by _keepAliveConnection,
    /// see class doc comment) there is no file, and it does not reliably wipe the data.
    /// The observed symptom when it silently failed: DataSeeder.SeedAsync() saw the OLD
    /// rooms were still there (`existingRooms.Any()` was true) and skipped reseeding,
    /// so a booking made by one test was still sitting there for the next test to collide
    /// with - which is exactly the "Room not available" conflict we chased down.
    ///
    /// Deleting rows directly is simpler and, unlike EnsureDeleted, actually reliable here.
    /// Order matters because of the FK constraints from AppDbContext (Bookings -> Rooms is
    /// Restrict, Services -> Rooms is Cascade): children must go before parents.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Bookings");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Services");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Rooms");

        var roomRepository = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        await DataSeeder.SeedAsync(roomRepository);
    }
}