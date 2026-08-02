using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IEventRepository
{
    Task<List<EventResult>> GetLocationEventsAsync(
        Guid locationId,
        string languageCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? skip,
        int? take,
        CancellationToken ct = default);

    // NOTE: One published event by id, with the church that hosts it. Null when it does not exist, is unpublished/soft-deleted, or its church is hidden.
    // NOTE: Deliberately not scoped to the caller - a share link has to open for anyone signed in, follower or not.
    Task<EventDetailResult?> GetEventByIdAsync(Guid eventId, string languageCode, CancellationToken ct = default);

    Task<List<FollowedEventResult>> GetFollowedEventsAsync(
        Guid userId,
        string languageCode,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);
}
