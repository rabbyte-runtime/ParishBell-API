using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IAnnouncementRepository
{
    Task<List<AnnouncementResult>> GetLocationAnnouncementsAsync(
        Guid locationId,
        string languageCode,
        DateTime nowUtc,
        int? skip,
        int? take,
        CancellationToken ct = default);
}
