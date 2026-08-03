namespace ParishBell.Core.DTOs.User;

public class NotificationPageDto
{
    public List<NotificationDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

public class NotificationUnreadCountDto
{
    // NOTE: Unread rows the inbox would actually show - the tab badge binds straight to this.
    public int UnreadCount { get; set; }
}

public class NotificationDto
{
    public Guid NotificationId { get; set; }

    // NOTE: Event, Announcement, MassReminder or FeastDay - drives icon and tap target.
    public string Type { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;

    // NOTE: ISO-8601 UTC - list ordering and "2h ago".
    public string SentAt { get; set; } = default!;

    public bool IsRead { get; set; }

    // NOTE: Deep-link targets. Exactly which are populated depends on Type; the rest are null.
    // NOTE: Event and Announcement add their own id. FeastDay adds calendarId.
    // NOTE: MassReminder carries locationId plus scheduleId.
    public Guid? LocationId { get; set; }
    public Guid? EventId { get; set; }
    public Guid? AnnouncementId { get; set; }
    public Guid? CalendarId { get; set; }

    // NOTE: MassReminder only - the mass this was for, so the row opens that mass.
    public Guid? ScheduleId { get; set; }

    // NOTE: "yyyy-MM-dd" for MassReminder and FeastDay, so a tap lands on that day.
    // NOTE: Null on Event and Announcement, which carry their own typed id.
    public string? Date { get; set; }
}
