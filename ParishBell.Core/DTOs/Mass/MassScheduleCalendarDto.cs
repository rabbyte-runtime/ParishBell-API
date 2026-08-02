namespace ParishBell.Core.DTOs.Mass;

// NOTE: Shaped like FollowedEventsCalendarDto - the client merges both onto one month grid, so they echo the same window.
public class MassScheduleCalendarDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public List<MassOccurrenceDto> Items { get; set; } = [];
}

// NOTE: One mass on one date. A weekly schedule yields several of these across the month, all sharing a scheduleId.
public class MassOccurrenceDto
{
    // NOTE: The schedule behind this occurrence - not unique in the list. Pair it with Date for a stable key.
    public Guid ScheduleId { get; set; }

    // NOTE: The church this mass belongs to - the list interleaves them, so every occurrence carries its own.
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = default!;

    // NOTE: "yyyy-MM-dd" - the concrete day this mass falls on, already expanded from the weekly pattern.
    public string Date { get; set; } = default!;

    // NOTE: "HH:mm" 24-hour, local to the church.
    public string MassTime { get; set; } = default!;

    // NOTE: e.g. "Sinhala Mass", "Confession" - already in the requested language, English where untranslated.
    public string Label { get; set; } = default!;

    // NOTE: Seasonal/one-off mass rather than a weekly recurring one. Its validity window is already applied.
    public bool IsSpecial { get; set; }

    // NOTE: The caller's reminder for the underlying schedule - null when unset. Repeats on every occurrence of that mass.
    public MassReminderDto? Reminder { get; set; }
}

public class MassReminderDto
{
    public Guid ReminderId { get; set; }

    // NOTE: Push fires at (massTime - minutesBefore).
    public int MinutesBefore { get; set; }

    // NOTE: False when the user switched the reminder off but kept it - the bell renders off, not unset.
    public bool IsActive { get; set; }
}
