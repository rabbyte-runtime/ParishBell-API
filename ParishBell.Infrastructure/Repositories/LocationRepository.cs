using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class LocationRepository(ParishBellDbContext dbContext) : ILocationRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;
    private const string DefaultLanguageCode = "en";

    public async Task<List<LocationResult>> GetActiveLocationsAsync(string languageCode, decimal? minLat, decimal? maxLat,
    decimal? minLng, decimal? maxLng, string? q, decimal? userLat, decimal? userLng, int? skip, int? take, Guid? userId = null, CancellationToken ct = default)
    {
        // NOTE: Resolve the requested language + English to their UUIDs
        var langs = await _dbContext.Languages.AsNoTracking().Where(l => l.LanguageCode == languageCode || l.LanguageCode == DefaultLanguageCode)
            .Select(l => new { l.LanguageId, l.LanguageCode }).ToListAsync(ct);

        var englishId = langs.FirstOrDefault(l => l.LanguageCode == DefaultLanguageCode)?.LanguageId;
        var requestedId = langs.FirstOrDefault(l => l.LanguageCode == languageCode)?.LanguageId ?? englishId;

        // NOTE: Active approved locations — idx_locations_approved covers IsApproved + IsActive
        var locQuery = _dbContext.Locations.AsNoTracking().Where(l => l.IsApproved && l.IsActive && !l.IsRejected);

        // NOTE: Viewport filter — idx_locations_coords covers this
        if (minLat.HasValue) locQuery = locQuery.Where(l => l.Latitude >= minLat.Value);
        if (maxLat.HasValue) locQuery = locQuery.Where(l => l.Latitude <= maxLat.Value);
        if (minLng.HasValue) locQuery = locQuery.Where(l => l.Longitude >= minLng.Value);
        if (maxLng.HasValue) locQuery = locQuery.Where(l => l.Longitude <= maxLng.Value);

        // NOTE: Search against name and address in the requested language + English fallback
        if (!string.IsNullOrWhiteSpace(q))
        {
            // NOTE: Escape LIKE wildcards so user input is matched literally, then wrap for a contains search.
            var pattern = $"%{q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            var matchIds = _dbContext.LocationTranslations
                .AsNoTracking()
                .Where(t => (t.LanguageId == requestedId || t.LanguageId == englishId)
                        && (EF.Functions.ILike(t.Name, pattern) ||
                            (t.Address != null && EF.Functions.ILike(t.Address, pattern))))
                .Select(t => t.LocationId);

            locQuery = locQuery.Where(l => matchIds.Contains(l.LocationId));
        }

        // NOTE: Order by squared Euclidean distance (No PostGIS! Accurate enough for sorting...).
        // NOTE: Secondary sort by LocationId makes pagination pages stable and non-overlapping.
        IQueryable<Core.Entities.Location> orderedQuery;
        if (userLat.HasValue && userLng.HasValue)
        {
            var lat = userLat.Value;
            var lng = userLng.Value;
            orderedQuery = locQuery.OrderBy(l => (l.Latitude - lat) * (l.Latitude - lat) +
            (l.Longitude - lng) * (l.Longitude - lng)).ThenBy(l => l.LocationId);
        }
        else if (skip.HasValue)
        {
            // NOTE: Any pagination without a distance sort still needs a stable order by
            orderedQuery = locQuery.OrderBy(l => l.LocationId);
        }
        else
        {
            orderedQuery = locQuery;
        }

        if (skip.HasValue) orderedQuery = orderedQuery.Skip(skip.Value);
        if (take.HasValue) orderedQuery = orderedQuery.Take(take.Value);

        var locations = await orderedQuery
            .Select(l => new
            {
                l.LocationId,
                l.LocationTypeId,
                l.Latitude,
                l.Longitude,
                l.Phone,
                l.Email,
                l.Website,

                // NOTE: Carried from the type so the map does not need a second call to colour its pins.
                l.LocationType.PinColorHex
            })
            .ToListAsync(ct);

        if (locations.Count == 0)
            return [];

        var locationIds = locations.Select(l => l.LocationId).ToList();

        var translations = await _dbContext.LocationTranslations.AsNoTracking()
            .Where(t => locationIds.Contains(t.LocationId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.LocationId, t.LanguageId, t.Name, t.Address }).ToListAsync(ct);

        // NOTE: One query for the whole page rather than a follow lookup per card. Anonymous callers skip it entirely.
        var followedIds = userId is null
            ? []
            : await _dbContext.UserFollowedLocations.AsNoTracking()
                .Where(f => f.UserId == userId.Value && locationIds.Contains(f.LocationId))
                .Select(f => f.LocationId)
                .ToListAsync(ct);

        var result = new List<LocationResult>(locations.Count);
        foreach (var loc in locations)
        {
            var name =
                translations.FirstOrDefault(t => t.LocationId == loc.LocationId && t.LanguageId == requestedId)?.Name
                ?? translations.FirstOrDefault(t => t.LocationId == loc.LocationId && t.LanguageId == englishId)?.Name
                ?? string.Empty;

            var address =
                translations.FirstOrDefault(t => t.LocationId == loc.LocationId && t.LanguageId == requestedId)?.Address
                ?? translations.FirstOrDefault(t => t.LocationId == loc.LocationId && t.LanguageId == englishId)?.Address;

            result.Add(new LocationResult(
                loc.LocationId,
                loc.Latitude,
                loc.Longitude,
                loc.LocationTypeId,
                name,
                address,
                loc.Phone,
                loc.Email,
                loc.Website,
                followedIds.Contains(loc.LocationId),
                loc.PinColorHex
            ));
        }

        return result;
    }

    public async Task<List<LocationResult>> GetFollowedLocationsAsync(Guid userId, string languageCode, int? skip, int? take, CancellationToken ct = default)
    {
        // NOTE: Resolve the requested language + English to their UUIDs
        var langs = await _dbContext.Languages.AsNoTracking().Where(l => l.LanguageCode == languageCode || l.LanguageCode == DefaultLanguageCode)
            .Select(l => new { l.LanguageId, l.LanguageCode }).ToListAsync(ct);

        var englishId = langs.FirstOrDefault(l => l.LanguageCode == DefaultLanguageCode)?.LanguageId;
        var requestedId = langs.FirstOrDefault(l => l.LanguageCode == languageCode)?.LanguageId ?? englishId;

        // NOTE: Follows joined to still-live locations, most recently followed first.
        // idx_ufl_user covers the user filter; ThenBy LocationId keeps pagination pages stable.
        IQueryable<Core.Entities.UserFollowedLocation> followQuery = _dbContext.UserFollowedLocations
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.Location.IsApproved && f.Location.IsActive && !f.Location.IsRejected)
            .OrderByDescending(f => f.FollowedAt)
            .ThenBy(f => f.LocationId);

        if (skip.HasValue) followQuery = followQuery.Skip(skip.Value);
        if (take.HasValue) followQuery = followQuery.Take(take.Value);

        var locations = await followQuery
            .Select(f => new
            {
                f.Location.LocationId,
                f.Location.LocationTypeId,
                f.Location.Latitude,
                f.Location.Longitude,
                f.Location.Phone,
                f.Location.Email,
                f.Location.Website,
                f.Location.LocationType.PinColorHex
            })
            .ToListAsync(ct);

        if (locations.Count == 0)
            return [];

        var locationIds = locations.Select(l => l.LocationId).ToList();

        var translations = await _dbContext.LocationTranslations.AsNoTracking()
            .Where(t => locationIds.Contains(t.LocationId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.LocationId, t.LanguageId, t.Name, t.Address }).ToListAsync(ct);

        // NOTE: Preserve the followed-at ordering from the query above.
        var result = new List<LocationResult>(locations.Count);
        foreach (var loc in locations)
        {
            var name =
                translations.FirstOrDefault(t => t.LocationId == loc.LocationId && t.LanguageId == requestedId)?.Name
                ?? translations.FirstOrDefault(t => t.LocationId == loc.LocationId && t.LanguageId == englishId)?.Name
                ?? string.Empty;

            var address =
                translations.FirstOrDefault(t => t.LocationId == loc.LocationId && t.LanguageId == requestedId)?.Address
                ?? translations.FirstOrDefault(t => t.LocationId == loc.LocationId && t.LanguageId == englishId)?.Address;

            result.Add(new LocationResult(
                loc.LocationId,
                loc.Latitude,
                loc.Longitude,
                loc.LocationTypeId,
                name,
                address,
                loc.Phone,
                loc.Email,
                loc.Website,

                // NOTE: This list *is* the user's follows, so every row is followed by definition.
                true,
                loc.PinColorHex
            ));
        }

        return result;
    }

    public async Task<LocationDetailResult?> GetLocationByIdAsync(Guid locationId, string languageCode, DateOnly massFromDate, DateOnly massToDate, Guid? userId = null, CancellationToken ct = default)
    {
        // NOTE: Resolve the requested language + English to their UUIDs
        var langs = await _dbContext.Languages
            .AsNoTracking()
            .Where(l => l.LanguageCode == languageCode || l.LanguageCode == DefaultLanguageCode)
            .Select(l => new { l.LanguageId, l.LanguageCode })
            .ToListAsync(ct);

        var englishId = langs.FirstOrDefault(l => l.LanguageCode == DefaultLanguageCode)?.LanguageId;
        var requestedId = langs.FirstOrDefault(l => l.LanguageCode == languageCode)?.LanguageId ?? englishId;

        var location = await _dbContext.Locations
            .AsNoTracking()
            .Where(l => l.LocationId == locationId && l.IsApproved && l.IsActive && !l.IsRejected)
            .Select(l => new { l.LocationId, l.LocationTypeId, l.Latitude, l.Longitude, l.Phone, l.Email, l.Website, l.LocationType.PinColorHex })
            .FirstOrDefaultAsync(ct);

        if (location is null) return null;

        // NOTE: Name, description, and address — requested language with English fallback
        var translations = await _dbContext.LocationTranslations
            .AsNoTracking()
            .Where(t => t.LocationId == locationId && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.LanguageId, t.Name, t.Description, t.Address })
            .ToListAsync(ct);

        var name = translations.FirstOrDefault(t => t.LanguageId == requestedId)?.Name
                ?? translations.FirstOrDefault(t => t.LanguageId == englishId)?.Name
                ?? string.Empty;
        var description = translations.FirstOrDefault(t => t.LanguageId == requestedId)?.Description
                       ?? translations.FirstOrDefault(t => t.LanguageId == englishId)?.Description;
        var address = translations.FirstOrDefault(t => t.LanguageId == requestedId)?.Address
                   ?? translations.FirstOrDefault(t => t.LanguageId == englishId)?.Address;

        // NOTE: Gallery images ordered for display — idx_li_sort covers this
        var images = await _dbContext.LocationImages
            .AsNoTracking()
            .Where(i => i.LocationId == locationId)
            .OrderBy(i => i.SortOrder)
            .Select(i => new LocationImageResult(i.ImageId, i.ImageUrl, i.IsPrimary, i.SortOrder))
            .ToListAsync(ct);

        // NOTE: Active schedules only — idx_ms_active covers LocationId + IsActive
        // NOTE: A special is kept only when its window overlaps the requested range; the service does the day-level intersection.
        var rawSchedules = await _dbContext.MassSchedules
            .AsNoTracking()
            .Where(s => s.LocationId == locationId && s.IsActive
                     && (!s.IsSpecial
                         || ((s.ValidFrom == null || s.ValidFrom <= massToDate)
                          && (s.ValidTo == null || s.ValidTo >= massFromDate))))
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.MassTime)
            .ThenBy(s => s.ScheduleId)
            .Select(s => new { s.ScheduleId, s.DayOfWeek, s.MassTime, s.IsSpecial, s.ValidFrom, s.ValidTo })
            .ToListAsync(ct);

        var scheduleIds = rawSchedules.Select(s => s.ScheduleId).ToList();
        var scheduleTranslations = scheduleIds.Count > 0
            ? await _dbContext.MassScheduleTranslations
                .AsNoTracking()
                .Where(t => scheduleIds.Contains(t.ScheduleId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
                .Select(t => new { t.ScheduleId, t.LanguageId, t.Label })
                .ToListAsync(ct)
            : [];

        // IMPORTANT: Scoped to the caller - a reminder is personal. Anonymous callers skip the query and get no bells.
        var reminders = userId is null || scheduleIds.Count == 0
            ? []
            : await _dbContext.UserMassReminders
                .AsNoTracking()
                .Where(r => r.UserId == userId.Value && scheduleIds.Contains(r.ScheduleId))
                .Select(r => new { r.ReminderId, r.ScheduleId, r.MinutesBefore, r.IsActive })
                .ToListAsync(ct);

        var schedules = rawSchedules.Select(s =>
        {
            var label = scheduleTranslations.FirstOrDefault(t => t.ScheduleId == s.ScheduleId && t.LanguageId == requestedId)?.Label
                     ?? scheduleTranslations.FirstOrDefault(t => t.ScheduleId == s.ScheduleId && t.LanguageId == englishId)?.Label
                     ?? string.Empty;

            // NOTE: uq_user_schedule makes this at most one row per user and mass.
            var reminder = reminders.FirstOrDefault(r => r.ScheduleId == s.ScheduleId);

            // NOTE: The church is carried on every row so the shape matches the calendar's, even though it is redundant here.
            return new MassSchedulePatternResult(
                s.ScheduleId, locationId, name, s.DayOfWeek, s.MassTime, label, s.IsSpecial, s.ValidFrom, s.ValidTo,
                reminder is null ? null : new MassReminderResult(reminder.ReminderId, reminder.MinutesBefore, reminder.IsActive));
        }).ToList();

        // NOTE: Parish-pinned feast days with liturgical calendar info
        var rawFeastDays = await _dbContext.LocationFeastDays
            .AsNoTracking()
            .Where(f => f.LocationId == locationId)
            .Select(f => new
            {
                f.LocationFeastDayId,
                f.IsHighlighted,
                f.CalendarId,
                f.Calendar.IsHolyDay,
                f.Calendar.IsRecurringAnnually,
                f.Calendar.Month,
                f.Calendar.Day,
                f.Calendar.SpecificDate
            })
            .ToListAsync(ct);

        var calendarIds = rawFeastDays.Select(f => f.CalendarId).ToList();
        var calendarTranslations = calendarIds.Count > 0
            ? await _dbContext.LiturgicalCalendarTranslations
                .AsNoTracking()
                .Where(t => calendarIds.Contains(t.CalendarId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
                .Select(t => new { t.CalendarId, t.LanguageId, t.Title, t.Description })
                .ToListAsync(ct)
            : [];

        var feastDays = rawFeastDays.Select(f =>
        {
            var title = calendarTranslations.FirstOrDefault(t => t.CalendarId == f.CalendarId && t.LanguageId == requestedId)?.Title
                     ?? calendarTranslations.FirstOrDefault(t => t.CalendarId == f.CalendarId && t.LanguageId == englishId)?.Title
                     ?? string.Empty;
            var desc = calendarTranslations.FirstOrDefault(t => t.CalendarId == f.CalendarId && t.LanguageId == requestedId)?.Description
                    ?? calendarTranslations.FirstOrDefault(t => t.CalendarId == f.CalendarId && t.LanguageId == englishId)?.Description;
            return new FeastDayResult(f.LocationFeastDayId, f.IsHighlighted, f.IsHolyDay, f.IsRecurringAnnually, f.Month, f.Day, f.SpecificDate, title, desc);
        }).ToList();

        // NOTE: Folded in here so opening the detail sheet is one call - it used to cost a separate GET .../follow.
        var isFollowing = userId is not null
            && await _dbContext.UserFollowedLocations.AsNoTracking()
                .AnyAsync(f => f.UserId == userId.Value && f.LocationId == locationId, ct);

        return new LocationDetailResult(
            location.LocationId,
            location.LocationTypeId,
            location.Latitude,
            location.Longitude,
            location.Phone,
            location.Email,
            location.Website,
            name,
            description,
            address,
            images,
            schedules,
            feastDays,
            isFollowing,
            location.PinColorHex
        );
    }
}
