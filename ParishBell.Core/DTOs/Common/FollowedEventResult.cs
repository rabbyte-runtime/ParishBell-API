namespace ParishBell.Core.DTOs.Common;

// NOTE: LocationName, Title and Description are already resolved with English fallback by the repository.
public record FollowedEventResult(
    Guid EventId,
    Guid LocationId,
    string LocationName,
    DateOnly EventDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string Title,
    string? Description,
    List<EventImageResult> Images
);
