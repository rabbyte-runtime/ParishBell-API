namespace ParishBell.Core.DTOs.Common;

// NOTE: Fetched by id rather than through a location list, so it carries the church.
// NOTE: A push or share link opens this screen cold, with no context to inherit.
// NOTE: Title, Description and LocationName already have English fallback applied.
public record EventDetailResult(
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
