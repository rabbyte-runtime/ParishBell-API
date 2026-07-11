using ParishBell.Core.DTOs.Location;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class EventService(IEventRepository eventRepository) : IEventService
{
    private readonly IEventRepository _eventRepository = eventRepository;

    public async Task<EventPageDto> GetLocationEventsAsync(
        Guid locationId,
        string languageCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        bool paginate = page.HasValue;
        int resolvedPage = page ?? 1;
        int resolvedPageSize = pageSize ?? 20;

        // NOTE: Fetch one extra row to detect a next page without a separate COUNT query
        int? skip = paginate ? (resolvedPage - 1) * resolvedPageSize : null;
        int? take = paginate ? resolvedPageSize + 1 : null;

        var results = await _eventRepository.GetLocationEventsAsync(
            locationId, languageCode, fromDate, toDate, skip, take, ct);

        bool hasMore = paginate && results.Count > resolvedPageSize;
        if (hasMore) results = [.. results.Take(resolvedPageSize)];

        var items = results.Select(r => new EventDto
        {
            EventId = r.EventId,
            EventDate = r.EventDate.ToString("yyyy-MM-dd"),
            StartTime = r.StartTime?.ToString("HH:mm"),
            EndTime = r.EndTime?.ToString("HH:mm"),
            Title = r.Title,
            Description = r.Description,
            Images = [.. r.Images.Select(i => new EventImageDto
            {
                EventImageId = i.EventImageId,
                ImageUrl = i.ImageUrl,
                SortOrder = i.SortOrder
            })]
        }).ToList();

        return new EventPageDto
        {
            Items = items,
            Page = resolvedPage,
            PageSize = resolvedPageSize,
            HasMore = hasMore
        };
    }

    public async Task<FollowedEventsCalendarDto> GetFollowedEventsAsync(
        Guid userId,
        string languageCode,
        int month,
        int year,
        CancellationToken ct = default)
    {
        // NOTE: The calendar shows a whole month, so we bound the query to that month's first and last day.
        var fromDate = new DateOnly(year, month, 1);
        var toDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        var results = await _eventRepository.GetFollowedEventsAsync(userId, languageCode, fromDate, toDate, ct);

        var items = results.Select(r => new FollowedEventDto
        {
            EventId = r.EventId,
            LocationId = r.LocationId,
            LocationName = r.LocationName,
            EventDate = r.EventDate.ToString("yyyy-MM-dd"),
            StartTime = r.StartTime?.ToString("HH:mm"),
            EndTime = r.EndTime?.ToString("HH:mm"),
            Title = r.Title,
            Description = r.Description,
            Images = [.. r.Images.Select(i => new EventImageDto
            {
                EventImageId = i.EventImageId,
                ImageUrl = i.ImageUrl,
                SortOrder = i.SortOrder
            })]
        }).ToList();

        return new FollowedEventsCalendarDto
        {
            Month = month,
            Year = year,
            Items = items
        };
    }
}
