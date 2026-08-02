using ParishBell.Core.DTOs.Location;

namespace ParishBell.Core.Interfaces;

public interface ILocationService
{
    // NOTE: Returns a paged list of active locations for the map view and list view.
    Task<LocationPageDto> GetActiveLocationsAsync(string languageCode, decimal? minLat, decimal? maxLat, decimal? minLng, decimal? maxLng, string? q,
    decimal? userLat, decimal? userLng, int? page, int? pageSize, CancellationToken ct = default);

    // NOTE: Returns location details for the given LocationId
    Task<LocationDetailDto> GetLocationByIdAsync(Guid locationId, string languageCode, CancellationToken ct = default);

    // NOTE: Returns a paged list of locations the user follows, most recently followed first.
    Task<LocationPageDto> GetFollowedLocationsAsync(Guid userId, string languageCode, int? page, int? pageSize, CancellationToken ct = default);
}
