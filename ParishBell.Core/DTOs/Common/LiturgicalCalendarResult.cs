namespace ParishBell.Core.DTOs.Common;

public record LiturgicalCalendarResult(
    Guid CalendarId,
    int? Month,
    int? Day,
    DateOnly? SpecificDate,
    bool IsRecurringAnnually,
    bool IsHolyDay,
    string Title,
    string? Description
);
