using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class EventRepository(ParishBellDbContext dbContext) : IEventRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;
    private const string DefaultLanguageCode = "en";

    public async Task<List<EventResult>> GetLocationEventsAsync(
        Guid locationId,
        string languageCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? skip,
        int? take,
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

        // NOTE: Published active events — idx_ev_published covers LocationId + IsPublished + IsActive + EventDate
        var eventsQuery = _dbContext.Events
            .AsNoTracking()
            .Where(e => e.LocationId == locationId && e.IsPublished && e.IsActive);

        if (fromDate.HasValue) eventsQuery = eventsQuery.Where(e => e.EventDate >= fromDate.Value);
        if (toDate.HasValue) eventsQuery = eventsQuery.Where(e => e.EventDate <= toDate.Value);

        // NOTE: Ascending by date so upcoming events come first naturally.
        // ThenBy EventId keeps pagination pages stable and non-overlapping.
        IQueryable<Core.Entities.Event> orderedQuery = eventsQuery
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.EventId);

        if (skip.HasValue) orderedQuery = orderedQuery.Skip(skip.Value);
        if (take.HasValue) orderedQuery = orderedQuery.Take(take.Value);

        var events = await orderedQuery
            .Select(e => new { e.EventId, e.EventDate, e.StartTime, e.EndTime })
            .ToListAsync(ct);

        if (events.Count == 0)
            return [];

        var eventIds = events.Select(e => e.EventId).ToList();

        var translations = await _dbContext.EventTranslations
            .AsNoTracking()
            .Where(t => eventIds.Contains(t.EventId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.EventId, t.LanguageId, t.Title, t.Description })
            .ToListAsync(ct);

        // NOTE: Images ordered for display — idx_ei_event covers EventId + SortOrder
        var images = await _dbContext.EventImages
            .AsNoTracking()
            .Where(i => eventIds.Contains(i.EventId))
            .OrderBy(i => i.SortOrder)
            .Select(i => new { i.EventId, i.EventImageId, i.ImageUrl, i.SortOrder })
            .ToListAsync(ct);

        var result = new List<EventResult>(events.Count);
        foreach (var ev in events)
        {
            var title = translations.FirstOrDefault(t => t.EventId == ev.EventId && t.LanguageId == requestedId)?.Title
                     ?? translations.FirstOrDefault(t => t.EventId == ev.EventId && t.LanguageId == englishId)?.Title
                     ?? string.Empty;

            var description = translations.FirstOrDefault(t => t.EventId == ev.EventId && t.LanguageId == requestedId)?.Description
                           ?? translations.FirstOrDefault(t => t.EventId == ev.EventId && t.LanguageId == englishId)?.Description;

            var imgs = images
                .Where(i => i.EventId == ev.EventId)
                .Select(i => new EventImageResult(i.EventImageId, i.ImageUrl, i.SortOrder))
                .ToList();

            result.Add(new EventResult(ev.EventId, ev.EventDate, ev.StartTime, ev.EndTime, title, description, imgs));
        }

        return result;
    }

    public async Task<List<FollowedEventResult>> GetFollowedEventsAsync(
        Guid userId,
        string languageCode,
        DateOnly fromDate,
        DateOnly toDate,
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

        // NOTE: Locations the user follows that are still live — idx_ufl_user covers the user filter
        var followedLocationIds = await _dbContext.UserFollowedLocations
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.Location.IsApproved && f.Location.IsActive && !f.Location.IsRejected)
            .Select(f => f.LocationId)
            .ToListAsync(ct);

        if (followedLocationIds.Count == 0)
            return [];

        // NOTE: Published active events across all followed locations within the month window.
        // NOTE:  Ordered by date, then start time (all-day events first), then EventId for a stable order.
        var events = await _dbContext.Events
            .AsNoTracking()
            .Where(e => followedLocationIds.Contains(e.LocationId) && e.IsPublished && e.IsActive
                     && e.EventDate >= fromDate && e.EventDate <= toDate)
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.StartTime)
            .ThenBy(e => e.EventId)
            .Select(e => new { e.EventId, e.LocationId, e.EventDate, e.StartTime, e.EndTime })
            .ToListAsync(ct);

        if (events.Count == 0)
            return [];

        var eventIds = events.Select(e => e.EventId).ToList();
        var eventLocationIds = events.Select(e => e.LocationId).Distinct().ToList();

        var translations = await _dbContext.EventTranslations
            .AsNoTracking()
            .Where(t => eventIds.Contains(t.EventId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.EventId, t.LanguageId, t.Title, t.Description })
            .ToListAsync(ct);

        // NOTE: Location names for the badge in each calendar entry — requested language with English fallback
        var locationNames = await _dbContext.LocationTranslations
            .AsNoTracking()
            .Where(t => eventLocationIds.Contains(t.LocationId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.LocationId, t.LanguageId, t.Name })
            .ToListAsync(ct);

        // NOTE: Images ordered for display — idx_ei_event covers EventId + SortOrder
        var images = await _dbContext.EventImages
            .AsNoTracking()
            .Where(i => eventIds.Contains(i.EventId))
            .OrderBy(i => i.SortOrder)
            .Select(i => new { i.EventId, i.EventImageId, i.ImageUrl, i.SortOrder })
            .ToListAsync(ct);

        var result = new List<FollowedEventResult>(events.Count);
        foreach (var ev in events)
        {
            var title = translations.FirstOrDefault(t => t.EventId == ev.EventId && t.LanguageId == requestedId)?.Title
                     ?? translations.FirstOrDefault(t => t.EventId == ev.EventId && t.LanguageId == englishId)?.Title
                     ?? string.Empty;

            var description = translations.FirstOrDefault(t => t.EventId == ev.EventId && t.LanguageId == requestedId)?.Description
                           ?? translations.FirstOrDefault(t => t.EventId == ev.EventId && t.LanguageId == englishId)?.Description;

            var locationName = locationNames.FirstOrDefault(t => t.LocationId == ev.LocationId && t.LanguageId == requestedId)?.Name
                            ?? locationNames.FirstOrDefault(t => t.LocationId == ev.LocationId && t.LanguageId == englishId)?.Name
                            ?? string.Empty;

            var imgs = images
                .Where(i => i.EventId == ev.EventId)
                .Select(i => new EventImageResult(i.EventImageId, i.ImageUrl, i.SortOrder))
                .ToList();

            result.Add(new FollowedEventResult(
                ev.EventId, ev.LocationId, locationName, ev.EventDate, ev.StartTime, ev.EndTime, title, description, imgs));
        }

        return result;
    }
}
