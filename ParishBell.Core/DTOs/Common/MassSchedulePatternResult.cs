namespace ParishBell.Core.DTOs.Common;

// NOTE: A weekly mass slot as stored, before it is expanded onto real dates. Shared by both readers of mass times -
// NOTE:  the calendar across followed churches, and one church's own profile - so there is a single shape for the concept.
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

// NOTE: The caller's own reminder for that mass, when they have set one. Inactive rows still come back - the toggle is off, not absent.
public record MassReminderResult(
    Guid ReminderId,
    int MinutesBefore,
    bool IsActive
);
