using ConferenceRoomBooking.Domain.Entities;
using Xunit;

namespace ConferenceRoomBooking.Tests;

public class DomainEntityTests
{
    [Fact]
    public void ResolveServices_ThrowsForUnknownService()
    {
        var room = new Room("Зал C", 30, 1500m);
        room.AddOrUpdateService("Wi-Fi", 300m);

        Assert.Throws<InvalidOperationException>(() => room.ResolveServices(new[] { "Проєктор" }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SetCapacity_RejectsNonPositiveValues(int invalidCapacity)
    {
        var room = new Room("Зал C", 30, 1500m);
        Assert.Throws<ArgumentException>(() => room.SetCapacity(invalidCapacity));
    }

    [Fact]
    public void Booking_OverlapsWith_DetectsOverlappingRange()
    {
        var booking = new Booking(
            Guid.NewGuid(),
            new DateTime(2024, 9, 1, 10, 0, 0),
            new DateTime(2024, 9, 1, 12, 0, 0),
            Array.Empty<string>(),
            2000m);

        Assert.True(booking.OverlapsWith(new DateTime(2024, 9, 1, 11, 0, 0), new DateTime(2024, 9, 1, 13, 0, 0)));
        Assert.False(booking.OverlapsWith(new DateTime(2024, 9, 1, 12, 0, 0), new DateTime(2024, 9, 1, 13, 0, 0)));
    }

    [Fact]
    public void Booking_OverlapsWith_IgnoresCancelledBookings()
    {
        var booking = new Booking(
            Guid.NewGuid(),
            new DateTime(2024, 9, 1, 10, 0, 0),
            new DateTime(2024, 9, 1, 12, 0, 0),
            Array.Empty<string>(),
            2000m);

        booking.Cancel();

        Assert.False(booking.OverlapsWith(new DateTime(2024, 9, 1, 10, 0, 0), new DateTime(2024, 9, 1, 12, 0, 0)));
    }
}
