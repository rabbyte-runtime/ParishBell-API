using ParishBell.Core.Configuration;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.DTOs.Push;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class FeastDayNotificationService(
    IFeastDayNotificationRepository repository,
    IPushNotificationService pushService,
    IMessageCache messages,
    FeastDayPushSettings settings) : IFeastDayNotificationService
{
    private readonly IFeastDayNotificationRepository _repository = repository;
    private readonly IPushNotificationService _pushService = pushService;
    private readonly IMessageCache _messages = messages;
    private readonly FeastDayPushSettings _settings = settings;

    private const short FeastDayType = (short)NotificationType.FeastDay;

    public async Task<FeastDayPushResult> ProcessDueAsync(CancellationToken ct = default)
    {
        int enqueued = await EnqueueTodaysFeastsAsync(ct);
        var (delivered, failed) = await DeliverPendingAsync(ct);
        return new FeastDayPushResult(enqueued, delivered, failed);
    }

    private async Task<int> EnqueueTodaysFeastsAsync(CancellationToken ct)
    {
        // IMPORTANT: A feast is a calendar date at the church, so today means the church today.
        var localNow = DateTime.UtcNow.AddMinutes(_settings.LocalUtcOffsetMinutes);
        var today = DateOnly.FromDateTime(localNow);

        // NOTE: Nothing goes out before the send hour, or once the day is too far gone.
        var sendFrom = today.ToDateTime(new TimeOnly(_settings.SendAtLocalHour, 0));
        if (localNow < sendFrom || localNow > sendFrom.AddHours(_settings.LookbackHours))
            return 0;

        var feasts = await _repository.GetPinnedFeastDaysAsync(ct);
        var dueToday = feasts.Where(f => FallsOn(f, today)).ToList();
        if (dueToday.Count == 0) return 0;

        int enqueued = 0;

        foreach (var feast in dueToday)
        {
            var recipients = await _repository.GetRecipientsWithoutLogAsync(feast.LocationFeastDayId, feast.LocationId, today, ct);
            if (recipients.Count == 0) continue;

            var translations = await _repository.GetTranslationsAsync(feast.CalendarId, ct);

            var logs = recipients.Select(r =>
            {
                var (title, body) = BuildContent(translations, r);
                return new NotificationsLog
                {
                    UserId = r.UserId,
                    Type = FeastDayType,
                    ReferenceId = feast.LocationFeastDayId,
                    OccurrenceDate = today,
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

    // NOTE: A fixed feast falls on its own date, a recurring one on its month and day.
    private static bool FallsOn(DueFeastDay feast, DateOnly date)
    {
        if (!feast.IsRecurringAnnually)
            return feast.SpecificDate == date;

        return feast.Month == date.Month && feast.Day == date.Day;
    }

    // NOTE: The recipient language, then English, then any translation that exists.
    // NOTE: A feast with none still gets a usable push rather than being dropped.
    private (string Title, string Body) BuildContent(Dictionary<Guid, (string Title, string? Description)> translations, FeastDayRecipient recipient)
    {
        if (translations.TryGetValue(recipient.LanguageId, out var own))
            return (own.Title, own.Description ?? own.Title);

        if (translations.Count > 0)
        {
            var any = translations.Values.First();
            return (any.Title, any.Description ?? any.Title);
        }

        var fallback = _messages.GetText(MessageCodes.FeastDayPushFallbackTitle, recipient.LanguageCode);
        return (fallback, fallback);
    }

    private async Task<(int Delivered, int Failed)> DeliverPendingAsync(CancellationToken ct)
    {
        var pending = await _repository.GetPendingAsync(_settings.DeliverBatchSize, ct);
        if (pending.Count == 0) return (0, 0);

        var delivered = new List<Guid>(pending.Count);

        foreach (var item in pending)
        {
            var result = await _pushService.SendToUserAsync(item.UserId, ToNotification(item), ct);

            // NOTE: Done when a device got it, or there was nothing to deliver.
            // NOTE: Only a hard failure is retried.
            if (result.SuccessCount > 0 || result.FailureCount == 0)
                delivered.Add(item.NotificationId);
        }

        await _repository.MarkSentAsync(delivered, DateTime.UtcNow, ct);
        return (delivered.Count, pending.Count - delivered.Count);
    }

    private static PushNotification ToNotification(PendingNotification item)
    {
        var data = new Dictionary<string, string> { ["type"] = "feastDay" };

        // NOTE: Each is optional - the pinned feast may vanish between queueing and delivery.
        // NOTE: The client routes on date alone, so a thin payload still opens the right day.
        if (item.CalendarId is not null)
            data["calendarId"] = item.CalendarId.Value.ToString();

        if (item.LocationId is not null)
            data["locationId"] = item.LocationId.Value.ToString();

        if (item.OccurrenceDate is not null)
            data["date"] = item.OccurrenceDate.Value.ToString("yyyy-MM-dd");

        return new PushNotification { Title = item.Title, Body = item.Body, Data = data };
    }
}
