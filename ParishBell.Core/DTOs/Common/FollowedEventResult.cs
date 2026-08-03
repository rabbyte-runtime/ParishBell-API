namespace ParishBell.Core.DTOs.Common;

// NOTE: LocationName, Title and Description already have English fallback applied.
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
