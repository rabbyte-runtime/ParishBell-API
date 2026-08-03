namespace ParishBell.Core.DTOs.Common;

// NOTE: A row of the user inbox.
// NOTE: LocationId and CalendarId are resolved from the polymorphic reference_id.
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

    // NOTE: The day the row is about, as recorded when it was queued. Authoritative when present.
    public DateOnly? OccurrenceDate { get; init; }

    // NOTE: Mass reminders only - the weekly slot the reminder fired for.
    // NOTE: Reconstructs the date for rows written before occurrence_date existed.
    public int? MassDayOfWeek { get; init; }
    public TimeOnly? MassTime { get; init; }

    // NOTE: Feast days only - the entry own date, resolved against the SentAt year.
    public DateOnly? FeastSpecificDate { get; init; }
    public int? FeastMonth { get; init; }
    public int? FeastDayOfMonth { get; init; }
}
