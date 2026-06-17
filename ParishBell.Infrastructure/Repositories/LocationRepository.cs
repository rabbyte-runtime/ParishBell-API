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
    decimal? minLng, decimal? maxLng, string? q, decimal? userLat, decimal? userLng, int? skip, int? take, CancellationToken ct = default)
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
            var qTrimmed = q.Trim();
            var matchIds = _dbContext.LocationTranslations
                .AsNoTracking()
                .Where(t => (t.LanguageId == requestedId || t.LanguageId == englishId)
                        && (t.Name.Contains(qTrimmed, StringComparison.OrdinalIgnoreCase) ||
                            (t.Address != null && t.Address.Contains(qTrimmed, StringComparison.OrdinalIgnoreCase))))
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
                l.Website
            })
            .ToListAsync(ct);

        if (locations.Count == 0)
            return [];

        var locationIds = locations.Select(l => l.LocationId).ToList();

        var translations = await _dbContext.LocationTranslations.AsNoTracking()
            .Where(t => locationIds.Contains(t.LocationId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.LocationId, t.LanguageId, t.Name, t.Address }).ToListAsync(ct);

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
                loc.Website
            ));
        }

        return result;
    }
}
