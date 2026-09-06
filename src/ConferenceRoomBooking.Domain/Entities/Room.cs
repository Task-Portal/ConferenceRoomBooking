namespace ConferenceRoomBooking.Domain.Entities;

/// <summary>
/// Represents a conference room that can be rented out.
/// The entity is intentionally kept "rich": it protects its own invariants
/// (capacity must be positive, price must be positive, service names are unique)
/// instead of relying on callers to do it correctly every time.
/// </summary>
public class Room
{
    private readonly List<Service> _services = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Capacity { get; private set; }
    public decimal BaseHourlyRate { get; private set; }

    /// <summary>Soft-delete flag. Deleted rooms are hidden from search/booking but kept for reporting/history.</summary>
    public bool IsDeleted { get; private set; }

    public IReadOnlyCollection<Service> Services => _services.AsReadOnly();

    // Required by EF Core / serializers if the project is later moved to a real database.
    private Room() { }

    public Room(string name, int capacity, decimal baseHourlyRate, IEnumerable<Service>? services = null)
    {
        Id = Guid.NewGuid();
        Rename(name);
        SetCapacity(capacity);
        SetBaseHourlyRate(baseHourlyRate);

        if (services is not null)
        {
            foreach (var service in services)
            {
                AddOrUpdateService(service.Name, service.Price);
            }
        }
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Room name cannot be empty.", nameof(name));
        }

        Name = name.Trim();
    }

    public void SetCapacity(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentException("Room capacity must be greater than zero.", nameof(capacity));
        }

        Capacity = capacity;
    }

    public void SetBaseHourlyRate(decimal baseHourlyRate)
    {
        if (baseHourlyRate <= 0)
        {
            throw new ArgumentException("Base hourly rate must be greater than zero.", nameof(baseHourlyRate));
        }

        BaseHourlyRate = baseHourlyRate;
    }

    /// <summary>Adds a new service to the room, or updates the price if a service with the same name already exists.</summary>
    public void AddOrUpdateService(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Service name cannot be empty.", nameof(name));
        }

        if (price < 0)
        {
            throw new ArgumentException("Service price cannot be negative.", nameof(price));
        }

        var existing = _services.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.UpdatePrice(price);
            return;
        }

        // Id is already known here (it's `this.Id`) - no need to make callers pass it back in.
        _services.Add(new Service(name, price, Id));
    }

    public void RemoveService(string name)
    {
        _services.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public void MarkDeleted() => IsDeleted = true;

    /// <summary>Returns the requested services by name, throwing if any requested service does not exist on this room.</summary>
    public IReadOnlyCollection<Service> ResolveServices(IEnumerable<string> serviceNames)
    {
        var resolved = new List<Service>();
        foreach (var name in serviceNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var service = _services.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (service is null)
            {
                throw new InvalidOperationException($"Service '{name}' is not available in room '{Name}'.");
            }

            resolved.Add(service);
        }

        return resolved;
    }
}
