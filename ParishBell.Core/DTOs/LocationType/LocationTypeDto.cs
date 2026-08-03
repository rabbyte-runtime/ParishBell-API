namespace ParishBell.Core.DTOs.LocationType;

public class LocationTypeDto
{
    public Guid LocationTypeId { get; set; }
    public string LocationTypeCode { get; set; } = default!;
    public int SortOrder { get; set; }
    public string Name { get; set; } = default!;

    // NOTE: "#RRGGBB" for the map pin. Authoritative, so all three clients agree and reordering types cannot reshuffle colours.
    // NOTE: Null only for a type seeded before this column existed - fall back to a neutral pin rather than to list position.
    public string? PinColorHex { get; set; }
}