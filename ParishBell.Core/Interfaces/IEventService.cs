using ParishBell.Core.DTOs.Location;

namespace ParishBell.Core.Interfaces;

public interface IEventService
{
    Task<EventPageDto> GetLocationEventsAsync(
        Guid locationId,
        string languageCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? page,
        int? pageSize,
        CancellationToken ct = default);

    // NOTE: One event by id, for a push deep-link or a share link.
    // NOTE: Throws NotFound when it is gone or hidden.
    Task<EventDetailDto> GetEventByIdAsync(Guid eventId, string languageCode, CancellationToken ct = default);

    Task<FollowedEventsCalendarDto> GetFollowedEventsAsync(
        Guid userId,
        string languageCode,
        int month,
        int year,
        CancellationToken ct = default);
}
