using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class LocationTypeRepository(ParishBellDbContext dbContext) : ILocationTypeRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;
    private const string DefaultLanguageCode = "en";

    public async Task<List<LocationTypeResult>> GetActiveLocationTypesAsync(string languageCode, CancellationToken ct = default)
    {
        // NOTE: Resolve the requested language + English to their UUIDs
        var langs = await _dbContext.Languages
            .AsNoTracking()
            .Where(l => l.LanguageCode == languageCode || l.LanguageCode == DefaultLanguageCode)
            .Select(l => new { l.LanguageId, l.LanguageCode })
            .ToListAsync(ct);

        var englishId = langs.FirstOrDefault(l => l.LanguageCode == DefaultLanguageCode)?.LanguageId;
        var requestedId = langs.FirstOrDefault(l => l.LanguageCode == languageCode)?.LanguageId
                          ?? englishId;  // IMPORTANT: Unknown code defaults to English

        // NOTE: Active types, ordered for display
        var types = await _dbContext.LocationTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new { t.LocationTypeId, t.SortOrder })
            .ToListAsync(ct);

        if (types.Count == 0)
            return [];

        var typeIds = types.Select(t => t.LocationTypeId).ToList();

        // NOTE: Pull only the translations we might use
        var translations = await _dbContext.LocationTypeTranslations.AsNoTracking()
            .Where(tr => typeIds.Contains(tr.LocationTypeId) && (tr.LanguageId == requestedId || tr.LanguageId == englishId))
            .Select(tr => new { tr.LocationTypeId, tr.LanguageId, tr.Name }).ToListAsync(ct);

        // NOTE: Requested language if present, else English, else empty.
        var result = new List<LocationTypeResult>(types.Count);
        foreach (var t in types)
        {
            var name =
                translations.FirstOrDefault(x => x.LocationTypeId == t.LocationTypeId && x.LanguageId == requestedId)?.Name
                ?? translations.FirstOrDefault(x => x.LocationTypeId == t.LocationTypeId && x.LanguageId == englishId)?.Name
                ?? string.Empty;

            result.Add(new LocationTypeResult(t.LocationTypeId, t.SortOrder, name));
        }

        return result;
    }
}