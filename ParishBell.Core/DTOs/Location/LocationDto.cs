namespace ParishBell.Core.DTOs.Location;

public class LocationDto
{
    public Guid LocationId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public Guid LocationTypeId { get; set; }
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    // NOTE: Populated only when the caller supplies userLat/userLng. Null otherwise.
    public double? DistanceKm { get; set; }

    // NOTE: "#RRGGBB" for this church's map pin, carried from its type so the map needs no lookup table of its own.
    public string? PinColorHex { get; set; }

    // NOTE: Whether the caller follows this church, so the list and detail sheet render Join/Leave without a second call.
    // IMPORTANT: This endpoint is public - an anonymous caller always gets false, which is not the same as "unknown".
    public bool IsFollowing { get; set; }
}
