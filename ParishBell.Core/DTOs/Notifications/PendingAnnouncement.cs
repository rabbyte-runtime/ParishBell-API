namespace ParishBell.Core.DTOs.Notifications;

// NOTE: An active announcement that may need push notifications sent to its location's followers.
public class PendingAnnouncement
{
    public Guid AnnouncementId { get; set; }
    public Guid LocationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<AnnouncementTranslationText> Translations { get; set; } = [];
}

// NOTE: One language's push-relevant text for an announcement.
public class AnnouncementTranslationText
{
    public string LanguageCode { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Caption { get; set; }
}
