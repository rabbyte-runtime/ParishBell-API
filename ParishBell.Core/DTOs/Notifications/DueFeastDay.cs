namespace ParishBell.Core.DTOs.Notifications;

// NOTE: A parish-pinned feast day with the recipients it should reach and everything needed to word the push.
// NOTE: Title and Description are resolved per recipient language by the service, so the raw translations travel here.
public class DueFeastDay
{
    public Guid LocationFeastDayId { get; set; }
    public Guid CalendarId { get; set; }
    public Guid LocationId { get; set; }

    // NOTE: Either a fixed date or an annually recurring month/day - the service resolves which day it lands on.
    public bool IsRecurringAnnually { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    public DateOnly? SpecificDate { get; set; }
}

// NOTE: One follower of the church that pinned the feast, in the language their push should be written in.
public record FeastDayRecipient(Guid UserId, Guid LanguageId, string LanguageCode);

public record FeastDayPushResult(int Enqueued, int Delivered, int Failed);
