namespace ParishBell.Core.DTOs.Location;

public class FollowedEventsCalendarDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public List<FollowedEventDto> Items { get; set; } = [];
}

public class FollowedEventDto
{
    public Guid EventId { get; set; }
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = default!;
    // NOTE: "yyyy-MM-dd"
    public string EventDate { get; set; } = default!;
    // NOTE: "HH:mm" 24-hour — null when no specific time is set
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public List<EventImageDto> Images { get; set; } = [];
}
