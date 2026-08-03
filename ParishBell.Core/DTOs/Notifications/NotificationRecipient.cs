namespace ParishBell.Core.DTOs.Notifications;

// NOTE: A follower to notify, with their preferred language for localization.
public class NotificationRecipient
{
    public Guid UserId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
}
