using ParishBell.Core.DTOs.Location;

namespace ParishBell.Core.Interfaces;

public interface ILocationService
{
    // NOTE: Returns a paged list of active locations for the map view and list view.
    // NOTE: userId is optional because these endpoints are public.
    // NOTE: Supply it to resolve IsFollowing; omit it and every row comes back false.
    Task<LocationPageDto> GetActiveLocationsAsync(string languageCode, decimal? minLat, decimal? maxLat, decimal? minLng, decimal? maxLng, string? q,
    decimal? userLat, decimal? userLng, int? page, int? pageSize, Guid? userId = null, CancellationToken ct = default);

    // NOTE: Returns location details for the given LocationId, including whether the caller follows it.
    // NOTE: Mass times are dated occurrences, the same shape /mass/schedule returns.
    Task<LocationDetailDto> GetLocationByIdAsync(Guid locationId, string languageCode, DateOnly massFrom, DateOnly massTo, Guid? userId = null, CancellationToken ct = default);

    // NOTE: Returns a paged list of locations the user follows, most recently followed first.
    Task<LocationPageDto> GetFollowedLocationsAsync(Guid userId, string languageCode, int? page, int? pageSize, CancellationToken ct = default);
}
