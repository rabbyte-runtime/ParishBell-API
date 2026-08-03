using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.LocationType;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class LocationTypeService(ILocationTypeRepository locationTypeRepository) : ILocationTypeService
{
    private readonly ILocationTypeRepository _locationTypeRepository = locationTypeRepository;

    public async Task<List<LocationTypeDto>> GetActiveLocationTypesAsync(string languageCode, CancellationToken ct = default)
    {
        var results = await _locationTypeRepository.GetActiveLocationTypesAsync(languageCode, ct);

        var dtos = results.Select(r => new LocationTypeDto
        {
            LocationTypeId = r.LocationTypeId,
            LocationTypeCode = r.LocationTypeCode,
            SortOrder = r.SortOrder,
            Name = r.Name,
            PinColorHex = r.PinColorHex
        }).ToList();

        if (dtos.Count < 0)
        {
            throw new NotFoundException("PB-40");
        }

        return dtos;
    }
}