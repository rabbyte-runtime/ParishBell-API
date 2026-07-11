using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/locations")]
public class LocationsController(ILocationService locationService, IEventService eventService, ILocationFollowService followService, IMessageCache messages) : ControllerBase
{
    private readonly ILocationService _locationService = locationService;
    private readonly IEventService _eventService = eventService;
    private readonly ILocationFollowService _followService = followService;
    private readonly IMessageCache _messages = messages;

    // NOTE: GET /api/v1/locations/{locationId}
    // IMPORTANT: Public - no JWT required.
    [HttpGet("{locationId:guid}")]
    public async Task<IActionResult> GetLocation(Guid locationId, [FromHeader(Name = "Accept-Language")] string? acceptLanguage, CancellationToken ct)
    {
        var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
        var result = await _locationService.GetLocationByIdAsync(locationId, languageCode, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationDetailRetrieved, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: GET /api/v1/locations/{locationId}/events
    // IMPORTANT: Requires User JWT.
    // NOTE: fromDate/toDate are optional date filters ("yyyy-MM-dd"). Results ordered ascending by event date.
    [Authorize]
    [HttpGet("{locationId:guid}/events")]
    public async Task<IActionResult> GetLocationEvents(
        Guid locationId,
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
        var result = await _eventService.GetLocationEventsAsync(locationId, languageCode, fromDate, toDate, page, pageSize, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationEventsRetrieved, result);
        return StatusCode(response.Status, response);
    }

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

    // NOTE: GET /api/v1/locations/followed
    // IMPORTANT: Requires User JWT. Returns the locations the current user follows, most recently followed first.
    // NOTE: Supply page + pageSize (default 50) to lazy-load; omit both to return the full list.
    [Authorize]
    [HttpGet("followed")]
    public async Task<IActionResult> GetFollowedLocations([FromHeader(Name = "Accept-Language")] string? acceptLanguage, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
        var userId = User.GetUserId();
        var result = await _locationService.GetFollowedLocationsAsync(userId, languageCode, page, pageSize, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.FollowedLocationsRetrieved, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: GET /api/v1/locations/followed/events
    // IMPORTANT: Requires User JWT. Published events across every location the current user follows.
    // NOTE: Optional ?month=(1-12) and ?year= filter to a single month for the calendar UI; both default to the current UTC month/year.
    // NOTE: Returns a flat list ordered by date then start time, each entry tagged with its locationId and locationName.
    [Authorize]
    [HttpGet("followed/events")]
    public async Task<IActionResult> GetFollowedEvents(
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken ct)
    {
        var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var resolvedMonth = month ?? today.Month;
        var resolvedYear = year ?? today.Year;

        if (resolvedMonth is < 1 or > 12 || resolvedYear is < 1 or > 9999)
            throw new BadRequestException(MessageCodes.FollowedEventsInvalidFilter);

        var userId = User.GetUserId();
        var result = await _eventService.GetFollowedEventsAsync(userId, languageCode, resolvedMonth, resolvedYear, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.FollowedEventsRetrieved, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: GET /api/v1/locations/{locationId}/follow
    // IMPORTANT: Requires User JWT. Returns whether the current user follows the location.
    [Authorize]
    [HttpGet("{locationId:guid}/follow")]
    public async Task<IActionResult> GetFollowStatus(Guid locationId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _followService.GetFollowStatusAsync(userId, locationId, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationFollowStatusRetrieved, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: POST /api/v1/locations/{locationId}/follow
    // IMPORTANT: Requires User JWT. Idempotent — re-following returns success.
    [Authorize]
    [HttpPost("{locationId:guid}/follow")]
    public async Task<IActionResult> FollowLocation(Guid locationId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _followService.FollowLocationAsync(userId, locationId, ct);
        var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationFollowed, null);
        return StatusCode(response.Status, response);
    }

    // NOTE: DELETE /api/v1/locations/{locationId}/follow
    // IMPORTANT: Requires User JWT. Idempotent — unfollowing a non-followed location returns success.
    [Authorize]
    [HttpDelete("{locationId:guid}/follow")]
    public async Task<IActionResult> UnfollowLocation(Guid locationId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _followService.UnfollowLocationAsync(userId, locationId, ct);
        var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationUnfollowed, null);
        return StatusCode(response.Status, response);
    }
}
