using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/locations")]
public class LocationsController(ILocationService locationService, IMessageCache messages) : ControllerBase
{
    private readonly ILocationService _locationService = locationService;
    private readonly IMessageCache _messages = messages;

    // NOTE: GET /api/v1/locations
    // IMPORTANT: Public - no JWT required; used by the map and search before the user signs in.
    // NOTE: Optional ?q= searches name + address. Optional bbox params (minLat/maxLat/minLng/maxLng) limit to the visible viewport.
    // NOTE: Supply userLat + userLng to order by nearest-first and populate distanceKm in each item. Supply page + pageSize (default 100) to lazy-load the list.
    [HttpGet]
    public async Task<IActionResult> GetLocations([FromHeader(Name = "Accept-Language")] string? acceptLanguage, [FromQuery] decimal? minLat, [FromQuery] decimal? maxLat,
    [FromQuery] decimal? minLng, [FromQuery] decimal? maxLng, [FromQuery] string? q, [FromQuery] decimal? userLat, [FromQuery] decimal? userLng, [FromQuery] int? page,
    [FromQuery] int? pageSize, CancellationToken ct)
    {
        var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
        var result = await _locationService.GetActiveLocationsAsync(languageCode, minLat, maxLat, minLng, maxLng, q, userLat, userLng, page, pageSize, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationsRetrieved, result);
        return StatusCode(response.Status, response);
    }
}
