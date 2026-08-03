namespace ParishBell.Core.DTOs.Common;

// NOTE: A weekly mass slot as stored, before expansion onto real dates.
// NOTE: Shared by the followed-churches calendar and a single church profile.
// NOTE: LocationName and Label are already resolved with English fallback by the repository.
public record MassSchedulePatternResult(
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

// NOTE: The caller reminder for that mass, when set.
// NOTE: Inactive rows still come back - the toggle is off, not absent.
public record MassReminderResult(
    Guid ReminderId,
    int MinutesBefore,
    bool IsActive
);
