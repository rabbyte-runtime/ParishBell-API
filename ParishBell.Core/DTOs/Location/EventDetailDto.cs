namespace ParishBell.Core.DTOs.Location;

// NOTE: The event detail screen reached by id - from a push carrying eventId, or a parishbell://events/{id} share link (§5.8).
// NOTE: Same fields as EventDto plus the church, because nothing around the screen supplies it when opened cold.
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

    // NOTE: Photos exist only on past events (§5.2), so an upcoming one comes back with an empty gallery.
    public List<EventImageDto> Images { get; set; } = [];
}
