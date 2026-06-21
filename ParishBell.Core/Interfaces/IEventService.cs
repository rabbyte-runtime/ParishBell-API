using ParishBell.Core.DTOs.Location;

namespace ParishBell.Core.Interfaces;

public interface IEventService
{
    Task<EventPageDto> GetLocationEventsAsync(
        Guid locationId,
        string languageCode,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? page,
        int? pageSize,
        CancellationToken ct = default);
}
