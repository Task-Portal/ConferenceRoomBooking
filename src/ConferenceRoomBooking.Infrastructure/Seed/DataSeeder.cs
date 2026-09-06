using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Domain.Interfaces;

namespace ConferenceRoomBooking.Infrastructure.Seed;

/// <summary>Populates the postgres store with the starting data described in the requirements (Зал А/B/C).</summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IRoomRepository roomRepository, CancellationToken cancellationToken = default)
    {
        var rooms = await roomRepository.GetAllAsync(cancellationToken);
        if (rooms.Any())
            return;


        var roomA = new Room("Зал А", capacity: 50, baseHourlyRate: 2000m);
        roomA.AddOrUpdateService("Проєктор", 500m);
        roomA.AddOrUpdateService("Wi-Fi", 300m);
        roomA.AddOrUpdateService("Звук", 700m);

        var roomB = new Room("Зал B", capacity: 100, baseHourlyRate: 3500m);
        roomB.AddOrUpdateService("Проєктор", 500m);
        roomB.AddOrUpdateService("Wi-Fi", 300m);
        roomB.AddOrUpdateService("Звук", 700m);

        var roomC = new Room("Зал C", capacity: 30, baseHourlyRate: 1500m);
        roomC.AddOrUpdateService("Проєктор", 500m);
        roomC.AddOrUpdateService("Wi-Fi", 300m);

        await roomRepository.AddAsync(roomA, cancellationToken);
        await roomRepository.AddAsync(roomB, cancellationToken);
        await roomRepository.AddAsync(roomC, cancellationToken);
    }
}