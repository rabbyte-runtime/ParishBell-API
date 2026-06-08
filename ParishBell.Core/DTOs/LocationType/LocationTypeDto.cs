namespace ParishBell.Core.DTOs.LocationType;

public class LocationTypeDto
{
    public Guid LocationTypeId { get; set; }
    public int SortOrder { get; set; }
    public string Name { get; set; } = default!;
}