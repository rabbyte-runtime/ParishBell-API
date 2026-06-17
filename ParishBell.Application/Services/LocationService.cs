using ParishBell.Core.DTOs.Location;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class LocationService(ILocationRepository locationRepository) : ILocationService
{
    private readonly ILocationRepository _locationRepository = locationRepository;

    public async Task<LocationPageDto> GetActiveLocationsAsync(string languageCode, decimal? minLat, decimal? maxLat,
    decimal? minLng, decimal? maxLng, string? q, decimal? userLat, decimal? userLng, int? page, int? pageSize, CancellationToken ct = default)
    {
        bool paginate = page.HasValue;
        int resolvedPage = page ?? 1;
        int resolvedPageSize = pageSize ?? 100;

        // NOTE: Fetch one extra row so we can detect a next page without a separate count query
        int? skip = paginate ? (resolvedPage - 1) * resolvedPageSize : null;
        int? take = paginate ? resolvedPageSize + 1 : null;

        var results = await _locationRepository.GetActiveLocationsAsync(
            languageCode, minLat, maxLat, minLng, maxLng, q,
            userLat, userLng, skip, take, ct);

        bool hasMore = paginate && results.Count > resolvedPageSize;
        if (hasMore) results = [.. results.Take(resolvedPageSize)];

        bool withDistance = userLat.HasValue && userLng.HasValue;

        var items = results.Select(r => new LocationDto
        {
            LocationId = r.LocationId,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            LocationTypeId = r.LocationTypeId,
            Name = r.Name,
            Address = r.Address,
            Phone = r.Phone,
            Email = r.Email,
            Website = r.Website,
            DistanceKm = withDistance
                ? Math.Round(Haversine(userLat!.Value, userLng!.Value, r.Latitude, r.Longitude), 2)
                : null
        }).ToList();

        return new LocationPageDto
        {
            Items = items,
            Page = resolvedPage,
            PageSize = resolvedPageSize,
            HasMore = hasMore
        };
    }

    // NOTE: Private helper methods
    private static double Haversine(decimal userLat, decimal userLng, decimal locLat, decimal locLng)
    {
        const double R = 6371.0;
        var dLat = ToRad((double)(locLat - userLat));
        var dLng = ToRad((double)(locLng - userLng));
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad((double)userLat)) * Math.Cos(ToRad((double)locLat)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * (Math.PI / 180.0);
}
