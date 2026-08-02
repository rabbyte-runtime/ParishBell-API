namespace ParishBell.Core.DTOs.Common;

public record AnnouncementResult(
    Guid AnnouncementId,
    Guid LocationId,
    short MediaType,
    string MediaUrl,
    string? ThumbnailUrl,
    int DurationSeconds,
    string Title,
    string? Description,
    DateTime CreatedAt,
    DateTime ExpiresAt
);
