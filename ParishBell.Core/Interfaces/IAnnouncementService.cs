using ParishBell.Core.DTOs.Location;

namespace ParishBell.Core.Interfaces;

public interface IAnnouncementService
{
    Task<AnnouncementPageDto> GetLocationAnnouncementsAsync(
        Guid userId,
        Guid locationId,
        string languageCode,
        int? page,
        int? pageSize,
        CancellationToken ct = default);
}
