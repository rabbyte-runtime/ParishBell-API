using ParishBell.Core.DTOs.Location;

namespace ParishBell.Core.Interfaces;

public interface IAnnouncementService
{
    // NOTE: One announcement with a media URL minted now, not at list load.
    // NOTE: Same members-only gate as the list. NotFound once expired or removed.
    Task<AnnouncementDto> GetAnnouncementAsync(Guid userId, Guid announcementId, string languageCode, CancellationToken ct = default);

    Task<AnnouncementPageDto> GetLocationAnnouncementsAsync(
        Guid userId,
        Guid locationId,
        string languageCode,
        int? page,
        int? pageSize,
        CancellationToken ct = default);
}
