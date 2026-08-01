namespace ParishBell.Core.DTOs.User;

public class NotificationPageDto
{
    public List<NotificationDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

public class NotificationDto
{
    public Guid NotificationId { get; set; }

    // NOTE: "Event" | "Announcement" | "MassReminder" | "FeastDay" - drives the row icon and the tap target.
    public string Type { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;

    // NOTE: ISO-8601 UTC - list ordering and "2h ago".
    public string SentAt { get; set; } = default!;

    public bool IsRead { get; set; }

    // NOTE: Deep-link targets. Exactly which are populated depends on Type; the rest are null.
    //       Event -> locationId + eventId. Announcement -> locationId + announcementId.
    //       MassReminder -> locationId. FeastDay -> locationId + calendarId.
    public Guid? LocationId { get; set; }
    public Guid? EventId { get; set; }
    public Guid? AnnouncementId { get; set; }
    public Guid? CalendarId { get; set; }
}
