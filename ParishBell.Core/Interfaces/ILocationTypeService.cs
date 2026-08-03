using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.LocationType;

namespace ParishBell.Core.Interfaces;

public interface ILocationTypeService
{
    // NOTE: Active types by sort order, name resolved with English fallback.
    Task<List<LocationTypeDto>> GetActiveLocationTypesAsync(string languageCode, CancellationToken ct = default);
}