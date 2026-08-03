namespace ParishBell.Core.DTOs.Notifications;

// NOTE: An active reminder, with everything needed to judge due-ness and word it.
// NOTE: LocationName and Label are already resolved in the user's own language by the repository.
public class DueMassReminder
{
    public Guid UserId { get; set; }

    // NOTE: The user's preferred language, so the body is worded for them rather than for the server.
    public string LanguageCode { get; set; } = "en";

    public int MinutesBefore { get; set; }

    public Guid ScheduleId { get; set; }
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    // NOTE: 0=Sunday..6=Saturday, plus the wall-clock time at the church.
    public int DayOfWeek { get; set; }
    public TimeOnly MassTime { get; set; }

    // NOTE: A special only fires inside its own window; a weekly one ignores these.
    public bool IsSpecial { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
}

// NOTE: One reminder matched to the day it fell due on.
// NOTE: That pair is what notifications_log is deduplicated by.
public record DueMassOccurrence(DueMassReminder Reminder, DateOnly OccurrenceDate);

public record MassReminderPushResult(int Enqueued, int Delivered, int Failed);
