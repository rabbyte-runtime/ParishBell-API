namespace ParishBell.Core.DTOs.Common;

// NOTE: One reminder with the mass behind it, for the standalone reminders list.
// NOTE: Nothing else supplies the mass or church here, so the row carries both.
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

    // NOTE: A reminder outlives an unfollow, so the list flags churches still followed.
    bool IsFollowing
);
