namespace ParishBell.Core.DTOs.Notifications;

// NOTE: A queued notifications_log row awaiting push delivery.
public class PendingNotification
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }

    // NOTE: The church the referenced entity belongs to, resolved at read time. Travels in the push
    //       payload so a tapped notification can open that church directly.
    public Guid? LocationId { get; set; }

    // NOTE: The day the notification is about, for recurring kinds. Travels in the push so a tap can open that date.
    public DateOnly? OccurrenceDate { get; set; }
}
