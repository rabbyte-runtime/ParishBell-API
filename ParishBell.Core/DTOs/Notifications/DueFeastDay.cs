namespace ParishBell.Core.DTOs.Notifications;

// NOTE: A parish-pinned feast day, with everything needed to word the push.
// NOTE: The service resolves them per recipient, so raw translations travel here.
public class DueFeastDay
{
    public Guid LocationFeastDayId { get; set; }
    public Guid CalendarId { get; set; }
    public Guid LocationId { get; set; }

    // NOTE: A fixed date or a recurring month and day - the service resolves it.
    public bool IsRecurringAnnually { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    public DateOnly? SpecificDate { get; set; }
}

// NOTE: One follower of the pinning church, in the language their push needs.
public record FeastDayRecipient(Guid UserId, Guid LanguageId, string LanguageCode);

public record FeastDayPushResult(int Enqueued, int Delivered, int Failed);
