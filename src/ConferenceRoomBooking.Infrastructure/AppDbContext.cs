using ConferenceRoomBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ConferenceRoomBooking.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Service> Services => Set<Service>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Room>(builder =>
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever(); // Guid is generated in the domain constructor, not by the DB.
            builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
            builder.Property(r => r.Capacity).IsRequired();
            builder.Property(r => r.BaseHourlyRate).HasColumnType("decimal(18,2)");

            // Services is exposed as IReadOnlyCollection<Service> and backed by the private
            // field `_services` - tell EF to materialize/read through that field directly,
            // since there is no public setter it could otherwise use.
            builder.Navigation(r => r.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Booking>(builder =>
        {
            builder.HasKey(booking => booking.Id);
            builder.Property(b => b.Id).ValueGeneratedNever();
            builder.Property(b => b.RoomId).IsRequired();
            builder.Property(b => b.StartTime).IsRequired();
            builder.Property(b => b.EndTime).IsRequired();
            builder.Property(b => b.TotalPrice).HasColumnType("decimal(18,2)");
            builder.Property(b => b.CustomerName).HasMaxLength(200);

            // Formal FK to Room: the database itself now rejects a booking pointing at a
            // RoomId that doesn't exist. DeleteBehavior.Restrict (not Cascade) is deliberate:
            // Booking and Room are separate aggregates (no navigation property is exposed on
            // either side), and a booking is a historical record that must survive even if
            // someone tries to remove its room - the app only ever soft-deletes rooms
            // (Room.IsDeleted) for exactly this reason, and Restrict backs that up at the DB
            // level in case a room is ever hard-deleted directly (e.g. manual SQL, a future
            // admin "purge" feature).
            builder.HasOne<Room>()
                .WithMany()
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // IReadOnlyCollection<string> has no way for EF to materialize/mutate it directly,
            // so it's stored as a single comma-separated column and converted back on read.
            // A ValueComparer is required for any collection-typed converted property so EF's
            // change tracker can tell whether it actually changed (it can't just use `==`).
            builder.Property(b => b.SelectedServiceNames)
                .HasConversion(
                    toDb => string.Join(',', toDb),
                    fromDb => string.IsNullOrEmpty(fromDb)
                        ? Array.Empty<string>()
                        : fromDb.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Metadata.SetValueComparer(new ValueComparer<IReadOnlyCollection<string>>(
                    (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
                    v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                    v => v.ToList()));
        });

        modelBuilder.Entity<Service>(builder =>
        {
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
            builder.Property(s => s.Price).HasColumnType("decimal(18,2)");
            builder.HasOne<Room>().WithMany(r => r.Services).HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
