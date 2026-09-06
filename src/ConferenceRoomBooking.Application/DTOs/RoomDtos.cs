using System.ComponentModel.DataAnnotations;

namespace ConferenceRoomBooking.Application.DTOs;

public sealed record ServiceDto(
    [param: Required, MinLength(1)] string Name,
    [param: Range(0, double.MaxValue)] decimal Price);

public sealed record RoomDto(
    Guid Id,
    string Name,
    int Capacity,
    decimal BaseHourlyRate,
    IReadOnlyCollection<ServiceDto> Services);

/// <summary>Request body for POST /api/rooms.</summary>
public sealed class CreateRoomRequest
{
    [Required(ErrorMessage = "Room name is required.")]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Range(1, 10000, ErrorMessage = "Capacity must be a positive number.")]
    public int Capacity { get; init; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Base hourly rate must be greater than zero.")]
    public decimal BaseHourlyRate { get; init; }

    public List<ServiceDto> Services { get; init; } = new();
}

/// <summary>
/// Request body for PATCH /api/rooms/{id}. All fields are optional - only supplied
/// fields are updated, which lets clients change a single value (e.g. just the price)
/// without resending the whole room, as described in the requirements.
/// </summary>
public sealed class UpdateRoomRequest
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; init; }

    [Range(1, 10000)]
    public int? Capacity { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal? BaseHourlyRate { get; init; }

    /// <summary>Services to add or update (matched by name). Existing services not listed here are left untouched.</summary>
    public List<ServiceDto>? ServicesToAddOrUpdate { get; init; }

    /// <summary>Names of services to remove from the room.</summary>
    public List<string>? ServiceNamesToRemove { get; init; }
}

public sealed class RoomAvailabilityRequest
{
    [Required]
    public DateTime StartTime { get; init; }

    [Required]
    public DateTime EndTime { get; init; }

    [Range(1, 10000)]
    public int MinCapacity { get; init; } = 1;
}
