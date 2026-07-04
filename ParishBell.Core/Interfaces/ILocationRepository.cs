using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface ILocationRepository
{
    Task<List<LocationResult>> GetActiveLocationsAsync(string languageCode, decimal? minLat, decimal? maxLat,
    decimal? minLng, decimal? maxLng, string? q, decimal? userLat, decimal? userLng, int? skip, int? take, CancellationToken ct = default);

    Task<LocationDetailResult?> GetLocationByIdAsync(Guid locationId, string languageCode, CancellationToken ct = default);

    // NOTE: Locations the user follows that are still live, most recently followed first.
    Task<List<LocationResult>> GetFollowedLocationsAsync(Guid userId, string languageCode, int? skip, int? take, CancellationToken ct = default);
}
