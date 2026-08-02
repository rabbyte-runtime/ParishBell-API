namespace ParishBell.Core.DTOs.Common;

// NOTE: One event fetched by id rather than through its location's list, so it carries the church itself -
// NOTE:  a push or a share link opens this screen cold, with no surrounding context to inherit.
// NOTE: Title, Description and LocationName are already resolved with English fallback by the repository.
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
