namespace ParishBell.Core.DTOs.Location;

public class LocationPageDto
{
    public List<LocationDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}
