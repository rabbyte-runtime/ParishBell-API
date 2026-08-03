using ParishBell.Core.DTOs.Location;

namespace ParishBell.Core.Interfaces;

public interface IAnnouncementService
{
    // NOTE: One announcement with a freshly minted media URL, for the moment the user presses play rather than when the list loaded.
    // NOTE: Same joined-members-only gate as the list. Throws NotFound once the post has expired or been removed.
    Task<AnnouncementDto> GetAnnouncementAsync(Guid userId, Guid announcementId, string languageCode, CancellationToken ct = default);

    Task<AnnouncementPageDto> GetLocationAnnouncementsAsync(
        Guid userId,
        Guid locationId,
        string languageCode,
        int? page,
        int? pageSize,
        CancellationToken ct = default);
}
