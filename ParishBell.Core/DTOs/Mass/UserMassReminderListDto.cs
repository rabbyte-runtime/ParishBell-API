namespace ParishBell.Core.DTOs.Mass;

public class UserMassReminderListDto
{
    public List<UserMassReminderDto> Items { get; set; } = [];
}

// NOTE: A weekly agenda row for the "my reminders" screen - the reminder plus the mass it belongs to.
public class UserMassReminderDto
{
    public Guid ReminderId { get; set; }

    // NOTE: Push fires at (massTime - minutesBefore).
    public int MinutesBefore { get; set; }

    // NOTE: False when the user cancelled it. The row survives so it can be switched back on by re-saving.
    public bool IsActive { get; set; }

    // NOTE: The mass this reminder is for - POST /api/v1/mass/reminders takes this to re-time or revive it.
    public Guid ScheduleId { get; set; }

    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = default!;

    // NOTE: 0=Sunday .. 6=Saturday. This list is a weekly agenda, not a dated calendar.
    public int DayOfWeek { get; set; }

    // NOTE: "HH:mm" 24-hour, local to the church.
    public string MassTime { get; set; } = default!;

    // NOTE: e.g. "Sinhala Mass" - already in the requested language, English where untranslated.
    public string Label { get; set; } = default!;

    // NOTE: Seasonal/one-off mass. Its window is exposed rather than applied, since this list has no month to clip against.
    public bool IsSpecial { get; set; }

    // NOTE: "yyyy-MM-dd". Null on weekly entries. A validTo in the past means the reminder will not fire again.
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }

    // NOTE: False when the user has since unfollowed that church. The reminder still fires - this is the only place it surfaces.
    public bool IsFollowing { get; set; }
}
