using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IAnnouncementRepository
{
    // NOTE: One announcement by id, under the same rules as the list. Null when lapsed.
    Task<AnnouncementResult?> GetAnnouncementAsync(Guid announcementId, string languageCode, DateTime nowUtc, CancellationToken ct = default);

    Task<List<AnnouncementResult>> GetLocationAnnouncementsAsync(
        Guid locationId,
        string languageCode,
        DateTime nowUtc,
        int? skip,
        int? take,
        CancellationToken ct = default);
}
