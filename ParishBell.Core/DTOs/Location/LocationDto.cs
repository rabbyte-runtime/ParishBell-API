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
}
