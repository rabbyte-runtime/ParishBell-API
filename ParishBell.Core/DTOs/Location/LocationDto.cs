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

    // NOTE: "#RRGGBB" for the map pin, carried from the type so the map needs no lookup.
    public string? PinColorHex { get; set; }

    // NOTE: Whether the caller follows this church, so Join/Leave needs no second call.
    // IMPORTANT: Public endpoint - anonymous callers get false, which is not "unknown".
    public bool IsFollowing { get; set; }
}
