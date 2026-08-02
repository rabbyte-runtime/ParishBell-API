namespace ParishBell.Core.DTOs.LocationType;

public class LocationTypeDto
{
    public Guid LocationTypeId { get; set; }
    public string LocationTypeCode { get; set; } = default!;
    public int SortOrder { get; set; }
    public string Name { get; set; } = default!;
}