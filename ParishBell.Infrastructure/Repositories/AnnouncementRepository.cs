using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class AnnouncementRepository(ParishBellDbContext dbContext) : IAnnouncementRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;
    private const string DefaultLanguageCode = "en";

    public async Task<List<AnnouncementResult>> GetLocationAnnouncementsAsync(
        Guid locationId,
        string languageCode,
        DateTime nowUtc,
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

        // NOTE: Active, unexpired posts for this location — idx_ann_active covers IsActive + ExpiresAt.
        // NOTE: Newest-first by CreatedAt so it reads like a chat/WhatsApp channel.
        //       ThenByDescending AnnouncementId keeps pagination pages stable and non-overlapping.
        IQueryable<Core.Entities.Announcement> orderedQuery = _dbContext.Announcements
            .AsNoTracking()
            .Where(a => a.LocationId == locationId && a.IsActive && a.ExpiresAt > nowUtc)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AnnouncementId);

        if (skip.HasValue) orderedQuery = orderedQuery.Skip(skip.Value);
        if (take.HasValue) orderedQuery = orderedQuery.Take(take.Value);

        var announcements = await orderedQuery
            .Select(a => new
            {
                a.AnnouncementId,
                a.LocationId,
                a.MediaType,
                a.MediaUrl,
                a.ThumbnailUrl,
                a.DurationSeconds,
                a.CreatedAt,
                a.ExpiresAt
            })
            .ToListAsync(ct);

        if (announcements.Count == 0)
            return [];

        var announcementIds = announcements.Select(a => a.AnnouncementId).ToList();

        var translations = await _dbContext.AnnouncementTranslations
            .AsNoTracking()
            .Where(t => announcementIds.Contains(t.AnnouncementId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.AnnouncementId, t.LanguageId, t.Title, t.Description })
            .ToListAsync(ct);

        var result = new List<AnnouncementResult>(announcements.Count);
        foreach (var a in announcements)
        {
            var title = translations.FirstOrDefault(t => t.AnnouncementId == a.AnnouncementId && t.LanguageId == requestedId)?.Title
                     ?? translations.FirstOrDefault(t => t.AnnouncementId == a.AnnouncementId && t.LanguageId == englishId)?.Title
                     ?? string.Empty;

            var description = translations.FirstOrDefault(t => t.AnnouncementId == a.AnnouncementId && t.LanguageId == requestedId)?.Description
                           ?? translations.FirstOrDefault(t => t.AnnouncementId == a.AnnouncementId && t.LanguageId == englishId)?.Description;

            result.Add(new AnnouncementResult(
                a.AnnouncementId,
                a.LocationId,
                a.MediaType,
                a.MediaUrl,
                a.ThumbnailUrl,
                a.DurationSeconds,
                title,
                description,
                a.CreatedAt,
                a.ExpiresAt));
        }

        return result;
    }
}
