namespace ParishBell.Core.DTOs.Mass;

// NOTE: Shaped like FollowedEventsCalendarDto so both merge onto one month grid.
public class MassScheduleCalendarDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public List<MassOccurrenceDto> Items { get; set; } = [];
}

// NOTE: One mass on one date. A weekly schedule yields several, sharing a scheduleId.
public class MassOccurrenceDto
{
    // NOTE: Not unique in the list - pair it with Date for a stable key.
    public Guid ScheduleId { get; set; }

    // NOTE: The list interleaves churches, so every occurrence carries its own.
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = default!;

    // NOTE: "yyyy-MM-dd" - already expanded from the weekly pattern.
    public string Date { get; set; } = default!;

    // NOTE: "HH:mm" 24-hour, local to the church.
    public string MassTime { get; set; } = default!;

    // NOTE: e.g. "Sinhala Mass" - already translated, English where none exists.
    public string Label { get; set; } = default!;

    // NOTE: A seasonal or one-off mass. Its validity window is already applied.
    public bool IsSpecial { get; set; }

    // NOTE: The caller reminder for the schedule, null when unset.
    // NOTE: Repeats on every occurrence of that mass.
    public MassReminderDto? Reminder { get; set; }
}

public class MassReminderDto
{
    public Guid ReminderId { get; set; }

    // NOTE: Push fires at (massTime - minutesBefore).
    public int MinutesBefore { get; set; }

    // NOTE: False when switched off but kept - the bell renders off, not unset.
    public bool IsActive { get; set; }
}
