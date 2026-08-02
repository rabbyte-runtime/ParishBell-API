namespace ParishBell.Core.DTOs.Common;

// NOTE: One mass time at a followed church. LocationName and Label are already resolved with English fallback by the repository.
// NOTE: Distinct from MassScheduleResult, which serves one location's own detail page and so carries neither the church nor the caller's reminder.
public record FollowedMassScheduleResult(
    Guid ScheduleId,
    Guid LocationId,
    string LocationName,
    int DayOfWeek,
    TimeOnly MassTime,
    string Label,
    bool IsSpecial,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    MassReminderResult? Reminder
);

// NOTE: The caller's own reminder for that mass, when they have set one. Inactive rows still come back - the toggle is off, not absent.
public record MassReminderResult(
    Guid ReminderId,
    int MinutesBefore,
    bool IsActive
);
