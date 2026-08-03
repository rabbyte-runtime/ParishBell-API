namespace ParishBell.Core.DTOs.Mass;

public class UserMassReminderListDto
{
    public List<UserMassReminderDto> Items { get; set; } = [];
}

// NOTE: A weekly agenda row - the reminder plus the mass it belongs to.
public class UserMassReminderDto
{
    public Guid ReminderId { get; set; }

    // NOTE: Push fires at (massTime - minutesBefore).
    public int MinutesBefore { get; set; }

    // NOTE: False when cancelled. The row survives so re-saving switches it back on.
    public bool IsActive { get; set; }

    // NOTE: The mass this is for - POST /mass/reminders re-times or revives it.
    public Guid ScheduleId { get; set; }

    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = default!;

    // NOTE: 0=Sunday .. 6=Saturday. This list is a weekly agenda, not a dated calendar.
    public int DayOfWeek { get; set; }

    // NOTE: "HH:mm" 24-hour, local to the church.
    public string MassTime { get; set; } = default!;

    // NOTE: e.g. "Sinhala Mass" - already in the requested language, English where untranslated.
    public string Label { get; set; } = default!;

    // NOTE: A seasonal mass. The window is exposed, not applied - there is no month here.
    public bool IsSpecial { get; set; }

    // NOTE: "yyyy-MM-dd", null on weekly entries.
    // NOTE: A validTo in the past means the reminder will not fire again.
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }

    // NOTE: False when the user has since unfollowed that church.
    // NOTE: This is the only screen where such a reminder surfaces.
    public bool IsFollowing { get; set; }
}
