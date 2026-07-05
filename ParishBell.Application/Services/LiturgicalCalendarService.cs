using ParishBell.Core.DTOs.Liturgical;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class LiturgicalCalendarService(ILiturgicalCalendarRepository calendarRepository) : ILiturgicalCalendarService
{
    private readonly ILiturgicalCalendarRepository _calendarRepository = calendarRepository;

    public async Task<LiturgicalCalendarListDto> GetByMonthYearAsync(
        int month,
        int year,
        string languageCode,
        CancellationToken ct = default)
    {
        var results = await _calendarRepository.GetByMonthYearAsync(month, year, languageCode, ct);

        var items = results.Select(r => new LiturgicalCalendarEntryDto
        {
            CalendarId = r.CalendarId,
            Date = ResolveDate(r.IsRecurringAnnually, r.Month, r.Day, r.SpecificDate, year),
            Month = r.Month,
            Day = r.Day,
            SpecificDate = r.SpecificDate?.ToString("yyyy-MM-dd"),
            IsRecurringAnnually = r.IsRecurringAnnually,
            IsHolyDay = r.IsHolyDay,
            Title = r.Title,
            Description = r.Description
        }).ToList();

        return new LiturgicalCalendarListDto
        {
            Month = month,
            Year = year,
            Items = items
        };
    }

    // NOTE: Resolves the concrete occurrence date within the requested year.
    // NOTE: Recurring entries are pinned to (year, month, day); a day that doesn't exist in that year (e.g. Feb 29 in a non-leap year) yields null rather than throwing.
    private static string? ResolveDate(bool isRecurringAnnually, int? month, int? day, DateOnly? specificDate, int year)
    {
        if (isRecurringAnnually)
        {
            if (month is >= 1 and <= 12 && day.HasValue &&
                day.Value >= 1 && day.Value <= DateTime.DaysInMonth(year, month.Value))
            {
                return new DateOnly(year, month.Value, day.Value).ToString("yyyy-MM-dd");
            }
            return null;
        }

        return specificDate?.ToString("yyyy-MM-dd");
    }
}
