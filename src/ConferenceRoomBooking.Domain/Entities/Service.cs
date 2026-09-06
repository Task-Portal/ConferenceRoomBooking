namespace ConferenceRoomBooking.Domain.Entities;

/// <summary>
/// An additional paid service that can be attached to a room (projector, Wi-Fi, sound system, etc.).
/// The price is flat per booking, matching the requirements ("проєктор, вартість 500 гривень").
/// </summary>
public class Service
{
    public Guid Id { get; private set; }
    public Guid RoomId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }

    private Service()
    {
    }

    public Service(string name, decimal price,  Guid roomId)
    {
        Id=Guid.NewGuid();
        RoomId = roomId;
        UpdateName(name);
        UpdatePrice(price);
    }

    private void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Service name cannot be empty.", nameof(name));
        Name = name.Trim();
    }

    public void UpdatePrice(decimal price)
    {
        if (price < 0)
        {
            throw new ArgumentException("Service price cannot be negative.", nameof(price));
        }

        Price = price;
    }
}