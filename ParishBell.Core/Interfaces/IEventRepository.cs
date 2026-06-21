using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IEventRepository
{
    Task<List<EventResult>> GetLocationEventsAsync(
        Guid locationId,
        string languageCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? skip,
        int? take,
        CancellationToken ct = default);
}
