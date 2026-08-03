namespace ParishBell.Core.DTOs.LocationType;

public class LocationTypeDto
{
    public Guid LocationTypeId { get; set; }
    public string LocationTypeCode { get; set; } = default!;
    public int SortOrder { get; set; }
    public string Name { get; set; } = default!;

    // NOTE: "#RRGGBB" for the map pin, so all three clients agree on the colour.
    // NOTE: Reordering types cannot reshuffle colours the way list position did.
    // NOTE: Null only for a type seeded before this column - fall back to a neutral pin.
    public string? PinColorHex { get; set; }
}