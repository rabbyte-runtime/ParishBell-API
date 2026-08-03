using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface ILocationRepository
{
    // NOTE: userId is optional because these endpoints are public.
    // NOTE: Supply it to resolve IsFollowing; omit it and every row comes back false.
    Task<List<LocationResult>> GetActiveLocationsAsync(string languageCode, decimal? minLat, decimal? maxLat,
    decimal? minLng, decimal? maxLng, string? q, decimal? userLat, decimal? userLng, int? skip, int? take, Guid? userId = null, CancellationToken ct = default);

    // NOTE: Mass times come back as weekly patterns; the service expands them onto dates.
    Task<LocationDetailResult?> GetLocationByIdAsync(Guid locationId, string languageCode, DateOnly massFromDate, DateOnly massToDate, Guid? userId = null, CancellationToken ct = default);

    // NOTE: Locations the user follows that are still live, most recently followed first.
    Task<List<LocationResult>> GetFollowedLocationsAsync(Guid userId, string languageCode, int? skip, int? take, CancellationToken ct = default);
}
