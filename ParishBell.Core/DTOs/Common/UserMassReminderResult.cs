namespace ParishBell.Core.DTOs.Common;

// NOTE: One of the user's reminders with the mass behind it, for the standalone "my reminders" list -
// NOTE:  unlike the calendar, nothing here supplies the mass or the church, so the row carries both.
// NOTE: LocationName and Label are already resolved with English fallback by the repository.
public record UserMassReminderResult(
    Guid ReminderId,
    int MinutesBefore,
    bool IsActive,
    Guid ScheduleId,
    Guid LocationId,
    string LocationName,
    int DayOfWeek,
    TimeOnly MassTime,
    string Label,
    bool IsSpecial,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,

    // NOTE: A reminder outlives an unfollow and keeps firing, so the list has to say which churches the user still follows.
    bool IsFollowing
);
