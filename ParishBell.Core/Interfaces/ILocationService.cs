using ParishBell.Core.DTOs.Location;

namespace ParishBell.Core.Interfaces;

public interface ILocationService
{
    // NOTE: Returns a paged list of active locations for the map view and list view.
    Task<LocationPageDto> GetActiveLocationsAsync(string languageCode, decimal? minLat, decimal? maxLat, decimal? minLng, decimal? maxLng, string? q,
    decimal? userLat, decimal? userLng, int? page, int? pageSize, CancellationToken ct = default);
}
