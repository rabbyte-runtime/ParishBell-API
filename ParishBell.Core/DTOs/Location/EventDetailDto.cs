namespace ParishBell.Core.DTOs.Location;

// NOTE: The event detail screen reached by id, from a push or a share link.
// NOTE: Same fields as EventDto plus the church, since nothing supplies it when cold.
public class EventDetailDto
{
    public Guid EventId { get; set; }

    // NOTE: The church hosting it - the screen shows the name and taps through to the profile.
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = default!;

    // NOTE: "yyyy-MM-dd"
    public string EventDate { get; set; } = default!;

    // NOTE: "HH:mm" 24-hour — null when no specific time is set
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }

    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    // NOTE: Photos exist only on past events, so upcoming ones have an empty gallery.
    public List<EventImageDto> Images { get; set; } = [];
}
