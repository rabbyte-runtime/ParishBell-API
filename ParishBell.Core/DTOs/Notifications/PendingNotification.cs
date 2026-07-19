namespace ParishBell.Core.DTOs.Notifications;

// NOTE: A queued notifications_log row awaiting push delivery.
public class PendingNotification
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
}
