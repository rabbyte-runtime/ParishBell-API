namespace ParishBell.Core.DTOs.Notifications;

// NOTE: A follower who should receive a notification, with their preferred language for localization.
public class NotificationRecipient
{
    public Guid UserId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
}
