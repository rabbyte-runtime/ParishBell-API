using ParishBell.Core.DTOs.Location;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;
using ParishBell.Core.Constants;

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

    public async Task<LocationDetailDto> GetLocationByIdAsync(Guid locationId, string languageCode, CancellationToken ct = default)
    {
        var result = await _locationRepository.GetLocationByIdAsync(locationId, languageCode, ct)
            ?? throw new NotFoundException(MessageCodes.LocationNotFound);

        return new LocationDetailDto
        {
            LocationId = result.LocationId,
            LocationTypeId = result.LocationTypeId,
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            Name = result.Name,
            Description = result.Description,
            Address = result.Address,
            Phone = result.Phone,
            Email = result.Email,
            Website = result.Website,
            Images = [.. result.Images.Select(i => new LocationImageDto
            {
                ImageId = i.ImageId,
                ImageUrl = i.ImageUrl,
                IsPrimary = i.IsPrimary,
                SortOrder = i.SortOrder
            })],
            MassSchedules = [.. result.Schedules.Select(s => new MassScheduleDto
            {
                ScheduleId = s.ScheduleId,
                DayOfWeek = s.DayOfWeek,
                MassTime = s.MassTime.ToString("HH:mm"),
                IsSpecial = s.IsSpecial,
                ValidFrom = s.ValidFrom?.ToString("yyyy-MM-dd"),
                ValidTo = s.ValidTo?.ToString("yyyy-MM-dd"),
                Label = s.Label
            })],
            FeastDays = [.. result.FeastDays.Select(f => new LocationFeastDayDto
            {
                LocationFeastDayId = f.LocationFeastDayId,
                IsHighlighted = f.IsHighlighted,
                Title = f.Title,
                Description = f.Description,
                IsHolyDay = f.IsHolyDay,
                IsRecurringAnnually = f.IsRecurringAnnually,
                Month = f.Month,
                Day = f.Day,
                SpecificDate = f.SpecificDate?.ToString("yyyy-MM-dd")
            })]
        };
    }

    public async Task<LocationPageDto> GetFollowedLocationsAsync(Guid userId, string languageCode, int? page, int? pageSize, CancellationToken ct = default)
    {
        bool paginate = page.HasValue;
        int resolvedPage = page ?? 1;
        int resolvedPageSize = pageSize ?? 50;

        // NOTE: Fetch one extra row so we can detect a next page without a separate count query
        int? skip = paginate ? (resolvedPage - 1) * resolvedPageSize : null;
        int? take = paginate ? resolvedPageSize + 1 : null;

        var results = await _locationRepository.GetFollowedLocationsAsync(userId, languageCode, skip, take, ct);

        bool hasMore = paginate && results.Count > resolvedPageSize;
        if (hasMore) results = [.. results.Take(resolvedPageSize)];

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
            // NOTE: No distance — the followed list isn't location-relative.
            DistanceKm = null
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
