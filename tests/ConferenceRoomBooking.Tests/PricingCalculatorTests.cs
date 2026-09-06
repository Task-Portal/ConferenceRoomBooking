using ConferenceRoomBooking.Application.Services;
using ConferenceRoomBooking.Domain.Entities;
using Xunit;

namespace ConferenceRoomBooking.Tests;

public class PricingCalculatorTests
{
    private readonly PricingCalculator _calculator = new();

    private static Room CreateRoomA() => new("Зал А", capacity: 50, baseHourlyRate: 2000m);

    [Fact]
    public void StandardHours_ChargesBaseRate()
    {
        var room = CreateRoomA();
        var start = new DateTime(2024, 9, 1, 10, 0, 0);
        var end = new DateTime(2024, 9, 1, 11, 0, 0);

        var result = _calculator.Calculate(room, start, end, Array.Empty<Service>());

        Assert.Equal(2000m, result.RoomRentalCost);
        Assert.Equal(2000m, result.TotalPrice);
    }

    [Fact]
    public void PeakHours_AppliesFifteenPercentSurcharge()
    {
        var room = CreateRoomA();
        var start = new DateTime(2024, 9, 1, 12, 0, 0);
        var end = new DateTime(2024, 9, 1, 13, 0, 0);

        var result = _calculator.Calculate(room, start, end, Array.Empty<Service>());

        Assert.Equal(2300m, result.RoomRentalCost); // 2000 * 1.15
    }

    [Fact]
    public void MorningHours_AppliesTenPercentDiscount()
    {
        var room = CreateRoomA();
        var start = new DateTime(2024, 9, 1, 7, 0, 0);
        var end = new DateTime(2024, 9, 1, 8, 0, 0);

        var result = _calculator.Calculate(room, start, end, Array.Empty<Service>());

        Assert.Equal(1800m, result.RoomRentalCost); // 2000 * 0.90
    }

    [Fact]
    public void EveningHours_AppliesTwentyPercentDiscount()
    {
        var room = CreateRoomA();
        var start = new DateTime(2024, 9, 1, 19, 0, 0);
        var end = new DateTime(2024, 9, 1, 20, 0, 0);

        var result = _calculator.Calculate(room, start, end, Array.Empty<Service>());

        Assert.Equal(1600m, result.RoomRentalCost); // 2000 * 0.80
    }

    [Fact]
    public void BookingSpanningMultipleBands_SumsEachSegmentAtItsOwnRate()
    {
        var room = CreateRoomA();
        // 08:00-09:00 morning (-10%), 09:00-12:00 standard, 12:00-13:00 peak (+15%)
        var start = new DateTime(2024, 9, 1, 8, 0, 0);
        var end = new DateTime(2024, 9, 1, 13, 0, 0);

        var result = _calculator.Calculate(room, start, end, Array.Empty<Service>());

        // 1800 (morning) + 6000 (3h standard) + 2300 (peak) = 10100
        Assert.Equal(10100m, result.RoomRentalCost);
    }

    [Fact]
    public void SelectedServices_AreAddedAsFlatCostOnTopOfRoomRental()
    {
        var room = CreateRoomA();
        room.AddOrUpdateService("Проєктор", 500m);
        room.AddOrUpdateService("Wi-Fi", 300m);

        var start = new DateTime(2024, 9, 1, 10, 0, 0);
        var end = new DateTime(2024, 9, 1, 11, 0, 0);
        var services = room.ResolveServices(new[] { "Проєктор", "Wi-Fi" });

        var result = _calculator.Calculate(room, start, end, services);

        Assert.Equal(2000m, result.RoomRentalCost);
        Assert.Equal(800m, result.ServicesCost);
        Assert.Equal(2800m, result.TotalPrice);
    }

    [Fact]
    public void HalfHourBooking_IsProratedCorrectly()
    {
        var room = CreateRoomA();
        var start = new DateTime(2024, 9, 1, 10, 0, 0);
        var end = new DateTime(2024, 9, 1, 10, 30, 0);

        var result = _calculator.Calculate(room, start, end, Array.Empty<Service>());

        Assert.Equal(1000m, result.RoomRentalCost);
    }
}
