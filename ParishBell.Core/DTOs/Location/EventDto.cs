namespace ParishBell.Core.DTOs.Location;

public class EventPageDto
{
    public List<EventDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

public class EventDto
{
    public Guid EventId { get; set; }
    // NOTE: "yyyy-MM-dd"
    public string EventDate { get; set; } = default!;
    // NOTE: "HH:mm" 24-hour — null when no specific time is set
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public List<EventImageDto> Images { get; set; } = [];
}

public class EventImageDto
{
    public Guid EventImageId { get; set; }
    public string ImageUrl { get; set; } = default!;
    public int SortOrder { get; set; }
}
