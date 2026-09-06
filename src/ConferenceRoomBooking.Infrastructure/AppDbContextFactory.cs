using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ConferenceRoomBooking.Infrastructure;

/// <summary>
/// Used only by EF Core CLI tools (`dotnet ef migrations add`, `dotnet ef database update`).
/// Without this, `dotnet ef` falls back to building the entire Api host from Program.cs to
/// obtain an AppDbContext - which also runs our startup migration/seeding code against a
/// database that (during "migrations add") doesn't have the target schema yet. This factory
/// gives the tooling a bare-bones, connection-only context instead, so schema commands work
/// independently of whatever the app's own startup logic does.
///
/// Reads the connection string the same way the app does (appsettings.json in the Api project),
/// so both stay in sync automatically.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var apiProjectPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "ConferenceRoomBooking.Api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(apiProjectPath) ? apiProjectPath : Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "your connection string";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
