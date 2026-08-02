using ParishBell.Core.Configuration;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.DTOs.Push;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class MassReminderNotificationService(
    IMassReminderNotificationRepository repository,
    IPushNotificationService pushService,
    IMessageCache messages,
    MassReminderPushSettings settings) : IMassReminderNotificationService
{
    private readonly IMassReminderNotificationRepository _repository = repository;
    private readonly IPushNotificationService _pushService = pushService;
    private readonly IMessageCache _messages = messages;
    private readonly MassReminderPushSettings _settings = settings;

    private const short MassReminderType = (short)NotificationType.MassReminder;

    public async Task<MassReminderPushResult> ProcessDueAsync(CancellationToken ct = default)
    {
        int enqueued = await EnqueueDueRemindersAsync(ct);
        var (delivered, failed) = await DeliverPendingAsync(ct);
        return new MassReminderPushResult(enqueued, delivered, failed);
    }

    private async Task<int> EnqueueDueRemindersAsync(CancellationToken ct)
    {
        // IMPORTANT: mass_time is wall-clock at the church, so every comparison below happens in local time and only
        // IMPORTANT:  the stored timestamps stay UTC. Mixing the two is what would make pushes hours early or late.
        var localNow = DateTime.UtcNow.AddMinutes(_settings.LocalUtcOffsetMinutes);
        var windowStart = localNow.AddMinutes(-_settings.LookbackMinutes);

        // NOTE: A fire time in the window can belong to a mass on either side of midnight, so both days are candidates.
        var candidateDates = new[] { DateOnly.FromDateTime(windowStart), DateOnly.FromDateTime(localNow) }.Distinct().ToList();
        var candidateDays = candidateDates.Select(d => (int)d.DayOfWeek).Distinct().ToList();

        var reminders = await _repository.GetActiveRemindersAsync(candidateDays, ct);
        if (reminders.Count == 0) return 0;

        var due = reminders
            .SelectMany(r => candidateDates
                .Where(date => IsDue(r, date, windowStart, localNow))
                .Select(date => new DueMassOccurrence(r, date)))
            .ToList();

        if (due.Count == 0) return 0;

        // NOTE: Anti-join on (user, schedule, date), so a reminder is queued once however often the job polls.
        var already = await _repository.GetAlreadyNotifiedAsync(candidateDates.Min(), candidateDates.Max(), ct);

        var logs = due
            .Where(d => !already.Contains((d.Reminder.UserId, d.Reminder.ScheduleId, d.OccurrenceDate)))
            .Select(d => new NotificationsLog
            {
                UserId = d.Reminder.UserId,
                Type = MassReminderType,
                ReferenceId = d.Reminder.ScheduleId,
                OccurrenceDate = d.OccurrenceDate,
                Title = d.Reminder.LocationName,
                Body = BuildBody(d.Reminder),
                IsSent = false
            })
            .ToList();

        await _repository.AddLogsAsync(logs, ct);
        return logs.Count;
    }

    // NOTE: Due when the moment to notify - the mass time less the user's chosen lead - has just passed. The window is
    // NOTE:  half-open so a fire time is claimed by exactly one poll, and a long-past one is left alone as stale.
    private static bool IsDue(DueMassReminder reminder, DateOnly date, DateTime windowStart, DateTime localNow)
    {
        if ((int)date.DayOfWeek != reminder.DayOfWeek)
            return false;

        // NOTE: A special mass only exists inside its own window; a weekly one ignores whatever dates sit on the row.
        if (reminder.IsSpecial)
        {
            if (reminder.ValidFrom is { } from && date < from) return false;
            if (reminder.ValidTo is { } to && date > to) return false;
        }

        var fireAt = date.ToDateTime(reminder.MassTime).AddMinutes(-reminder.MinutesBefore);
        return fireAt > windowStart && fireAt <= localNow;
    }

    // NOTE: Wording lives in the messages table like every other user-facing string, so si/ta come from the same place.
    //       An unseeded code degrades to the bare "PB-87" rather than throwing, which is why the format call is guarded.
    private string BuildBody(DueMassReminder reminder)
    {
        var template = _messages.GetText(MessageCodes.MassReminderPushBody, reminder.LanguageCode);

        try
        {
            return string.Format(template, reminder.Label, reminder.MinutesBefore);
        }
        catch (FormatException)
        {
            // NOTE: A malformed template must not stop the push - the user still gets a usable reminder.
            return reminder.Label;
        }
    }

    // NOTE: Delivers queued rows one user at a time (SendToUser fans out across that user's devices).
    private async Task<(int Delivered, int Failed)> DeliverPendingAsync(CancellationToken ct)
    {
        var pending = await _repository.GetPendingAsync(_settings.DeliverBatchSize, ct);
        if (pending.Count == 0) return (0, 0);

        var delivered = new List<Guid>(pending.Count);

        foreach (var item in pending)
        {
            var result = await _pushService.SendToUserAsync(item.UserId, ToNotification(item), ct);

            // NOTE: Done when a device received it, or there was nothing to deliver (user has no devices).
            //       Only a hard failure is left for the next poll to retry.
            if (result.SuccessCount > 0 || result.FailureCount == 0)
                delivered.Add(item.NotificationId);
        }

        await _repository.MarkSentAsync(delivered, DateTime.UtcNow, ct);
        return (delivered.Count, pending.Count - delivered.Count);
    }

    private static PushNotification ToNotification(PendingNotification item)
    {
        var data = new Dictionary<string, string> { ["type"] = "massReminder" };

        // NOTE: The three ids the client deep-links on. Each is optional only because the mass could have been removed
        //       between queueing and delivery, so the client must treat a thin payload as "open the church, not the day".
        if (item.ReferenceId is not null)
            data["scheduleId"] = item.ReferenceId.Value.ToString();

        if (item.LocationId is not null)
            data["locationId"] = item.LocationId.Value.ToString();

        if (item.OccurrenceDate is not null)
            data["date"] = item.OccurrenceDate.Value.ToString("yyyy-MM-dd");

        return new PushNotification { Title = item.Title, Body = item.Body, Data = data };
    }
}
