using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.Location;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class AnnouncementService(
    IAnnouncementRepository announcementRepository,
    ILocationFollowRepository followRepository,
    IBlobUrlSigner urlSigner) : IAnnouncementService
{
    private readonly IAnnouncementRepository _announcementRepository = announcementRepository;
    private readonly ILocationFollowRepository _followRepository = followRepository;

    // NOTE: Media URLs carry a SAS baked in at upload, so they are re-signed on the way out.
    private readonly IBlobUrlSigner _urlSigner = urlSigner;

    public async Task<AnnouncementDto> GetAnnouncementAsync(Guid userId, Guid announcementId, string languageCode, CancellationToken ct = default)
    {
        var result = await _announcementRepository.GetAnnouncementAsync(announcementId, languageCode, DateTime.UtcNow, ct)
            ?? throw new NotFoundException(MessageCodes.AnnouncementNotFound);

        // IMPORTANT: The follow gate runs after the lookup so outsiders cannot probe for ids.
        // NOTE: A missing post and another church's channel look identical from outside.
        if (!await _followRepository.IsFollowingAsync(userId, result.LocationId, ct))
            throw new ForbiddenException(MessageCodes.LocationAnnouncementsForbidden);

        // NOTE: The whole point of the endpoint - a URL minted now, not at list load.
        return await MapAsync(result, ct);
    }

    public async Task<AnnouncementPageDto> GetLocationAnnouncementsAsync(
        Guid userId,
        Guid locationId,
        string languageCode,
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        // NOTE: A joined-members-only channel - the caller must follow the location.
        // NOTE: A non-existent location is covered too - you cannot follow one.
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

        // NOTE: Re-signed here too, so a list rendered now plays now.
        // NOTE: It still goes stale while the user reads - that is what the detail endpoint is for.
        var items = new List<AnnouncementDto>(results.Count);
        foreach (var r in results)
            items.Add(await MapAsync(r, ct));

        return new AnnouncementPageDto
        {
            Items = items,
            Page = resolvedPage,
            PageSize = resolvedPageSize,
            HasMore = hasMore
        };
    }

    // NOTE: The stored URL is re-signed. An external or CDN URL passes through untouched.
    private async Task<AnnouncementDto> MapAsync(AnnouncementResult r, CancellationToken ct) => new()
    {
        AnnouncementId = r.AnnouncementId,
        LocationId = r.LocationId,
        MediaType = ((MediaType)r.MediaType).ToString(),
        MediaUrl = await _urlSigner.ResignAsync(r.MediaUrl, ct) ?? r.MediaUrl,
        ThumbnailUrl = await _urlSigner.ResignAsync(r.ThumbnailUrl, ct),
        DurationSeconds = r.DurationSeconds,
        Title = r.Title,
        Description = r.Description,
        CreatedAt = FormatUtc(r.CreatedAt),
        ExpiresAt = FormatUtc(r.ExpiresAt)
    };

    // NOTE: DB timestamps are UTC - emit an explicit "Z" so the countdown is unambiguous.
    private static string FormatUtc(DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'");
}
