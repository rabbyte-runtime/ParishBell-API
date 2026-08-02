namespace ParishBell.Core.DTOs.Common;

// NOTE: A row of the user's inbox. LocationId and CalendarId are resolved from the polymorphic reference_id by the repository, since notifications_log stores neither.
public record NotificationResult
{
    public Guid NotificationId { get; init; }
    public short Type { get; init; }
    public string Title { get; init; } = null!;
    public string Body { get; init; } = null!;
    public DateTime SentAt { get; init; }
    public bool IsRead { get; init; }

    // NOTE: The entity this notification is about - meaning depends on Type.
    public Guid? ReferenceId { get; init; }

    // NOTE: The church behind that entity, when it has one.
    public Guid? LocationId { get; init; }

    // NOTE: Feast days only - the liturgical_calendar entry behind the location_feast_days row.
    public Guid? CalendarId { get; init; }

    // NOTE: Mass reminders only - the weekly slot the reminder fired for. notifications_log stores no date, so the
    //       occurrence is reconstructed from these plus SentAt.
    public int? MassDayOfWeek { get; init; }
    public TimeOnly? MassTime { get; init; }

    // NOTE: Feast days only - the calendar entry's own date, fixed or recurring, resolved against SentAt's year.
    public DateOnly? FeastSpecificDate { get; init; }
    public int? FeastMonth { get; init; }
    public int? FeastDayOfMonth { get; init; }
}
