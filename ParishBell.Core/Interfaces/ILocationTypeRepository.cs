using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface ILocationTypeRepository
{
    // NOTE: Returns active types ordered by sort order, name resolved to the requested language with English fallback
    Task<List<LocationTypeResult>> GetActiveLocationTypesAsync(string languageCode, CancellationToken ct = default);
}