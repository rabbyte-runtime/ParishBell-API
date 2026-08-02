using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class LiturgicalCalendarRepository(ParishBellDbContext dbContext) : ILiturgicalCalendarRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;
    private const string DefaultLanguageCode = "en";

    public async Task<List<LiturgicalCalendarResult>> GetByMonthYearAsync(
        int month,
        int year,
        string languageCode,
        CancellationToken ct = default)
    {
        // NOTE: Resolve the requested language + English to their UUIDs
        var langs = await _dbContext.Languages
            .AsNoTracking()
            .Where(l => l.LanguageCode == languageCode || l.LanguageCode == DefaultLanguageCode)
            .Select(l => new { l.LanguageId, l.LanguageCode })
            .ToListAsync(ct);

        var englishId = langs.FirstOrDefault(l => l.LanguageCode == DefaultLanguageCode)?.LanguageId;
        var requestedId = langs.FirstOrDefault(l => l.LanguageCode == languageCode)?.LanguageId ?? englishId;

        // NOTE: Two kinds of entries land in a given month/year:
        //   - Recurring annual entries whose month matches (they occur every year) — idx_lc_recurring
        //   - One-off entries whose specific_date falls in the requested month AND year — idx_lc_specific_date
        var entries = await _dbContext.LiturgicalCalendars
            .AsNoTracking()
            .Where(c =>
                (c.IsRecurringAnnually && c.Month == month) ||
                (!c.IsRecurringAnnually && c.SpecificDate.HasValue &&
                 c.SpecificDate.Value.Month == month && c.SpecificDate.Value.Year == year))
            .Select(c => new { c.CalendarId, c.Month, c.Day, c.SpecificDate, c.IsRecurringAnnually, c.IsHolyDay })
            .ToListAsync(ct);

        if (entries.Count == 0)
            return [];

        var calendarIds = entries.Select(e => e.CalendarId).ToList();

        var translations = await _dbContext.LiturgicalCalendarTranslations
            .AsNoTracking()
            .Where(t => calendarIds.Contains(t.CalendarId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.CalendarId, t.LanguageId, t.Title, t.Description })
            .ToListAsync(ct);

        // NOTE: Order by the day the entry falls on so the month reads chronologically.
        // NOTE: Recurring entries use Day; one-off entries use the day of their specific_date.
        var ordered = entries
            .OrderBy(e => e.IsRecurringAnnually ? e.Day ?? 0 : e.SpecificDate?.Day ?? 0)
            .ToList();

        var result = new List<LiturgicalCalendarResult>(ordered.Count);
        foreach (var e in ordered)
        {
            var title = translations.FirstOrDefault(t => t.CalendarId == e.CalendarId && t.LanguageId == requestedId)?.Title
                     ?? translations.FirstOrDefault(t => t.CalendarId == e.CalendarId && t.LanguageId == englishId)?.Title
                     ?? string.Empty;

            var description = translations.FirstOrDefault(t => t.CalendarId == e.CalendarId && t.LanguageId == requestedId)?.Description
                           ?? translations.FirstOrDefault(t => t.CalendarId == e.CalendarId && t.LanguageId == englishId)?.Description;

            result.Add(new LiturgicalCalendarResult(
                e.CalendarId, e.Month, e.Day, e.SpecificDate, e.IsRecurringAnnually, e.IsHolyDay, title, description));
        }

        return result;
    }
}
