using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.LocationType;

namespace ParishBell.Core.Interfaces;

public interface ILocationTypeService
{
    // NOTE: Returns active types ordered by sort order, name resolved to the requested language with English fallback
    Task<List<LocationTypeDto>> GetActiveLocationTypesAsync(string languageCode, CancellationToken ct = default);
}