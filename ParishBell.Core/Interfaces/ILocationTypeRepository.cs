using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface ILocationTypeRepository
{
    // NOTE: Active types by sort order, name resolved with English fallback.
    Task<List<LocationTypeResult>> GetActiveLocationTypesAsync(string languageCode, CancellationToken ct = default);
}