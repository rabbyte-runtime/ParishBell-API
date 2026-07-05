namespace ParishBell.Core.DTOs.Liturgical;

public class LiturgicalCalendarListDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public List<LiturgicalCalendarEntryDto> Items { get; set; } = [];
}

public class LiturgicalCalendarEntryDto
{
    public Guid CalendarId { get; set; }
    // NOTE: The occurrence date within the requested month/year ("yyyy-MM-dd").
    // NOTE: Null when it cannot be resolved (e.g. a recurring Feb 29 entry in a non-leap year).
    public string? Date { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    // NOTE: "yyyy-MM-dd" — set only for one-off entries.
    public string? SpecificDate { get; set; }
    public bool IsRecurringAnnually { get; set; }
    public bool IsHolyDay { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
}
