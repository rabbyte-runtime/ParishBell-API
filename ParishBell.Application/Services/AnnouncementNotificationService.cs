using ParishBell.Core.Configuration;
using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.DTOs.Push;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class AnnouncementNotificationService(
    IAnnouncementNotificationRepository repository,
    IPushNotificationService pushService,
    AnnouncementPushSettings settings) : IAnnouncementNotificationService
{
    private readonly IAnnouncementNotificationRepository _repository = repository;
    private readonly IPushNotificationService _pushService = pushService;
    private readonly AnnouncementPushSettings _settings = settings;

    private const short AnnouncementType = (short)NotificationType.Announcement;

    public async Task<AnnouncementPushResult> ProcessPendingAsync(CancellationToken ct = default)
    {
        int enqueued = await EnqueueNewAnnouncementsAsync(ct);
        var (delivered, failed) = await DeliverPendingAsync(ct);
        return new AnnouncementPushResult(enqueued, delivered, failed);
    }

    // NOTE: Queue one localized log row per follower who does not already have one.
    // NOTE: Idempotent across polls thanks to the anti-join.
    private async Task<int> EnqueueNewAnnouncementsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-_settings.LookbackMinutes);

        var announcements = await _repository.GetAnnouncementsToNotifyAsync(cutoff, now, ct);
        int enqueued = 0;

        foreach (var announcement in announcements)
        {
            var recipients = await _repository.GetFollowersWithoutLogAsync(announcement.AnnouncementId, announcement.LocationId, ct);
            if (recipients.Count == 0) continue;

            var logs = recipients.Select(r =>
            {
                var (title, body) = BuildContent(announcement, r.LanguageCode);
                return new NotificationsLog
                {
                    UserId = r.UserId,
                    Type = AnnouncementType,
                    ReferenceId = announcement.AnnouncementId,
                    Title = title,
                    Body = body,
                    IsSent = false
                };
            }).ToList();

            await _repository.AddLogsAsync(logs, ct);
            enqueued += logs.Count;
        }

        return enqueued;
    }

    // NOTE: Delivers queued rows one user at a time (SendToUser fans out across that user's devices).
    private async Task<(int Delivered, int Failed)> DeliverPendingAsync(CancellationToken ct)
    {
        var pending = await _repository.GetPendingAsync(AnnouncementType, _settings.DeliverBatchSize, ct);
        if (pending.Count == 0) return (0, 0);

        var delivered = new List<Guid>(pending.Count);

        foreach (var item in pending)
        {
            var result = await _pushService.SendToUserAsync(item.UserId, ToNotification(item), ct);

            // NOTE: Done when a device received it, or there was nothing to deliver (user has no devices).
            // NOTE: Only a hard failure is left for the next poll to retry.
            if (result.SuccessCount > 0 || result.FailureCount == 0)
                delivered.Add(item.NotificationId);
        }

        await _repository.MarkSentAsync(delivered, DateTime.UtcNow, ct);
        return (delivered.Count, pending.Count - delivered.Count);
    }

    private static PushNotification ToNotification(PendingNotification item)
    {
        if (item.ReferenceId is null)
            return new PushNotification { Title = item.Title, Body = item.Body };

        var data = new Dictionary<string, string>
        {
            ["type"] = "announcement",
            ["announcementId"] = item.ReferenceId.Value.ToString()
        };

        // NOTE: Lets a tapped push open the church's channel outright. Absent only when the announcement
        // NOTE: The client must treat it as optional.
        if (item.LocationId is not null)
            data["locationId"] = item.LocationId.Value.ToString();

        return new PushNotification { Title = item.Title, Body = item.Body, Data = data };
    }

    // NOTE: Recipient language, then English, then any translation, then a generic string.
    // NOTE: Announcement titles and captions are both optional in the schema.
    private static (string Title, string Body) BuildContent(PendingAnnouncement announcement, string languageCode)
    {
        var title = PickText(announcement.Translations, languageCode, t => t.Title);
        var body = PickText(announcement.Translations, languageCode, t => t.Caption);

        return (
            string.IsNullOrWhiteSpace(title) ? GenericTitle(languageCode) : title,
            string.IsNullOrWhiteSpace(body) ? GenericBody(languageCode) : body
        );
    }

    private static string? PickText(IReadOnlyList<AnnouncementTranslationText> translations, string languageCode, Func<AnnouncementTranslationText, string?> selector)
    {
        string? preferred = translations.FirstOrDefault(t => t.LanguageCode == languageCode) is { } m ? selector(m) : null;
        string? english = translations.FirstOrDefault(t => t.LanguageCode == "en") is { } e ? selector(e) : null;
        string? any = translations.Select(selector).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        return FirstNonBlank(preferred, english, any);
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string GenericTitle(string languageCode) => languageCode switch
    {
        "si" => "නව නිවේදනයක්",
        "ta" => "புதிய அறிவிப்பு",
        _ => "New announcement"
    };

    private static string GenericBody(string languageCode) => languageCode switch
    {
        "si" => "ඔබ අනුගමනය කරන දේවස්ථානයෙන් නව නිවේදනයක් ඇත.",
        "ta" => "நீங்கள் பின்தொடரும் ஆலயத்திலிருந்து புதிய அறிவிப்பு உள்ளது.",
        _ => "A new announcement is available from a church you follow."
    };
}
