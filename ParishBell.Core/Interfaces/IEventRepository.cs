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

    // NOTE: One published event by id, with the church that hosts it.
    // NOTE: Null when missing, unpublished, deleted, or its church is hidden.
    // NOTE: Not follower-scoped - a share link must open for anyone signed in.
    Task<EventDetailResult?> GetEventByIdAsync(Guid eventId, string languageCode, CancellationToken ct = default);

    Task<List<FollowedEventResult>> GetFollowedEventsAsync(
        Guid userId,
        string languageCode,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);
}
