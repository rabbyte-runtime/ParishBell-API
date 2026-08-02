namespace ParishBell.Core.DTOs.Common;

public record EventResult(
    Guid EventId,
    DateOnly EventDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string Title,
    string? Description,
    List<EventImageResult> Images
);

public record EventImageResult(Guid EventImageId, string ImageUrl, int SortOrder);
