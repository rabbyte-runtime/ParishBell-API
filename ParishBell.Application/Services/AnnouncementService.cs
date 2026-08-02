using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Location;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class AnnouncementService(
    IAnnouncementRepository announcementRepository,
    ILocationFollowRepository followRepository) : IAnnouncementService
{
    private readonly IAnnouncementRepository _announcementRepository = announcementRepository;
    private readonly ILocationFollowRepository _followRepository = followRepository;

    public async Task<AnnouncementPageDto> GetLocationAnnouncementsAsync(
        Guid userId,
        Guid locationId,
        string languageCode,
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        // NOTE: Announcements are a joined-members-only channel — the caller must follow the location.
        //       A non-existent location is naturally covered here: you cannot follow one.
        if (!await _followRepository.IsFollowingAsync(userId, locationId, ct))
            throw new ForbiddenException(MessageCodes.LocationAnnouncementsForbidden);

        bool paginate = page.HasValue;
        int resolvedPage = page ?? 1;
        int resolvedPageSize = pageSize ?? 20;

        // NOTE: Fetch one extra row to detect a next page without a separate COUNT query
        int? skip = paginate ? (resolvedPage - 1) * resolvedPageSize : null;
        int? take = paginate ? resolvedPageSize + 1 : null;

        var results = await _announcementRepository.GetLocationAnnouncementsAsync(
            locationId, languageCode, DateTime.UtcNow, skip, take, ct);

        bool hasMore = paginate && results.Count > resolvedPageSize;
        if (hasMore) results = [.. results.Take(resolvedPageSize)];

        var items = results.Select(r => new AnnouncementDto
        {
            AnnouncementId = r.AnnouncementId,
            LocationId = r.LocationId,
            MediaType = ((MediaType)r.MediaType).ToString(),
            MediaUrl = r.MediaUrl,
            ThumbnailUrl = r.ThumbnailUrl,
            DurationSeconds = r.DurationSeconds,
            Title = r.Title,
            Description = r.Description,
            CreatedAt = FormatUtc(r.CreatedAt),
            ExpiresAt = FormatUtc(r.ExpiresAt)
        }).ToList();

        return new AnnouncementPageDto
        {
            Items = items,
            Page = resolvedPage,
            PageSize = resolvedPageSize,
            HasMore = hasMore
        };
    }

    // NOTE: DB timestamps are stored as UTC; emit an explicit "Z" so the client countdown is unambiguous.
    private static string FormatUtc(DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'");
}
