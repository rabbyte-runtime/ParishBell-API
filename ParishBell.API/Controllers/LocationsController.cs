using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/locations")]
public class LocationsController(
    ILocationService locationService,
    IEventService eventService,
    ILocationFollowService followService,
    IAnnouncementService announcementService,
    IMessageCache messages,
    ILogger<LocationsController> logger) : ApiControllerBase(logger)
{
    private readonly ILocationService _locationService = locationService;
    private readonly IEventService _eventService = eventService;
    private readonly ILocationFollowService _followService = followService;
    private readonly IAnnouncementService _announcementService = announcementService;
    private readonly IMessageCache _messages = messages;

    // NOTE: Seven days inclusive of today - the "what's on this week" the church profile's mass tab shows.
    private const int DefaultMassWindowDays = 6;

    // NOTE: Room for a month view without letting a caller ask for years of expanded occurrences.
    private const int MaxMassWindowDays = 62;

    // NOTE: GET /api/v1/locations/{locationId}
    // IMPORTANT: Public - no JWT required.
    // NOTE: isFollowing is folded in when a token is present, so the detail sheet opens in one call instead of also hitting GET .../follow.
    // NOTE: An anonymous caller always gets isFollowing:false - that is "not signed in", not "not following".
    // NOTE: massSchedules are dated occurrences, the same shape GET /api/v1/mass/schedule returns - the client never projects weekdays onto dates itself.
    // NOTE: Optional ?massFrom=&massTo= ("yyyy-MM-dd") pick the window; it defaults to the coming week. Capped at 62 days, and an inverted range is a 400.
    // NOTE: Each occurrence carries the caller's reminder when a token is present, so the profile's bells work like the calendar's.
    [HttpGet("{locationId:guid}")]
    public Task<IActionResult> GetLocation(
        Guid locationId,
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        [FromQuery] DateOnly? massFrom,
        [FromQuery] DateOnly? massTo,
        CancellationToken ct) =>
        ExecuteAsync(nameof(GetLocation), async () =>
        {
            var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();

            // NOTE: "This week" is what the profile's mass tab asks for, so that is what an unqualified call returns.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var resolvedFrom = massFrom ?? today;
            var resolvedTo = massTo ?? resolvedFrom.AddDays(DefaultMassWindowDays);

            // IMPORTANT: Expansion is linear in the window, so an unbounded range would let one call fan out indefinitely.
            if (resolvedTo < resolvedFrom || resolvedFrom.AddDays(MaxMassWindowDays) < resolvedTo)
                throw new BadRequestException(MessageCodes.MassScheduleInvalidRange);

            var result = await _locationService.GetLocationByIdAsync(locationId, languageCode, resolvedFrom, resolvedTo, User.GetUserIdOrNull(), ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationDetailRetrieved, result);
            return StatusCode(response.Status, response);
        }, locationId, massFrom, massTo);

    // NOTE: GET /api/v1/locations/{locationId}/events
    // IMPORTANT: Requires User JWT.
    // NOTE: fromDate/toDate are optional date filters ("yyyy-MM-dd"). Results ordered ascending by event date.
    [Authorize]
    [HttpGet("{locationId:guid}/events")]
    public Task<IActionResult> GetLocationEvents(
        Guid locationId,
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct) =>
        ExecuteAsync(nameof(GetLocationEvents), async () =>
        {
            var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
            var result = await _eventService.GetLocationEventsAsync(locationId, languageCode, fromDate, toDate, page, pageSize, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationEventsRetrieved, result);
            return StatusCode(response.Status, response);
        }, locationId, fromDate, toDate);

    // NOTE: GET /api/v1/locations/{locationId}/announcements
    // IMPORTANT: Requires User JWT. Joined-members-only channel — 403 if the caller doesn't follow the location.
    // NOTE: Time-limited audio/video posts, newest first. Expired posts are filtered out server-side.
    // NOTE: Supply page + pageSize (default 20) to lazy-load; omit both to return the full active list.
    [Authorize]
    [HttpGet("{locationId:guid}/announcements")]
    public Task<IActionResult> GetLocationAnnouncements(
        Guid locationId,
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct) =>
        ExecuteAsync(nameof(GetLocationAnnouncements), async () =>
        {
            var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
            var userId = User.GetUserId();
            var result = await _announcementService.GetLocationAnnouncementsAsync(userId, locationId, languageCode, page, pageSize, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationAnnouncementsRetrieved, result);
            return StatusCode(response.Status, response);
        }, locationId);

    // NOTE: GET /api/v1/locations
    // IMPORTANT: Public - no JWT required; used by the map and search before the user signs in.
    // NOTE: Optional ?q= searches name + address. Optional bbox params (minLat/maxLat/minLng/maxLng) limit to the visible viewport.
    // NOTE: Supply userLat + userLng to order by nearest-first and populate distanceKm in each item. Supply page + pageSize (default 100) to lazy-load the list.
    // NOTE: isFollowing is resolved in one batched query when a token is present, so the list needs no follow lookup per card.
    [HttpGet]
    public Task<IActionResult> GetLocations([FromHeader(Name = "Accept-Language")] string? acceptLanguage, [FromQuery] decimal? minLat, [FromQuery] decimal? maxLat,
    [FromQuery] decimal? minLng, [FromQuery] decimal? maxLng, [FromQuery] string? q, [FromQuery] decimal? userLat, [FromQuery] decimal? userLng, [FromQuery] int? page,
    [FromQuery] int? pageSize, CancellationToken ct) =>
        ExecuteAsync(nameof(GetLocations), async () =>
        {
            var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
            var result = await _locationService.GetActiveLocationsAsync(languageCode, minLat, maxLat, minLng, maxLng, q, userLat, userLng, page, pageSize, User.GetUserIdOrNull(), ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationsRetrieved, result);
            return StatusCode(response.Status, response);
        }, q, page, pageSize);

    // NOTE: GET /api/v1/locations/followed
    // IMPORTANT: Requires User JWT. Returns the locations the current user follows, most recently followed first.
    // NOTE: Supply page + pageSize (default 50) to lazy-load; omit both to return the full list.
    [Authorize]
    [HttpGet("followed")]
    public Task<IActionResult> GetFollowedLocations([FromHeader(Name = "Accept-Language")] string? acceptLanguage, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        ExecuteAsync(nameof(GetFollowedLocations), async () =>
        {
            var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
            var userId = User.GetUserId();
            var result = await _locationService.GetFollowedLocationsAsync(userId, languageCode, page, pageSize, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.FollowedLocationsRetrieved, result);
            return StatusCode(response.Status, response);
        }, page, pageSize);

    // NOTE: GET /api/v1/locations/followed/events
    // IMPORTANT: Requires User JWT. Published events across every location the current user follows.
    // NOTE: Optional ?month=(1-12) and ?year= filter to a single month for the calendar UI; both default to the current UTC month/year.
    // NOTE: Returns a flat list ordered by date then start time, each entry tagged with its locationId and locationName.
    [Authorize]
    [HttpGet("followed/events")]
    public Task<IActionResult> GetFollowedEvents(
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken ct) =>
        ExecuteAsync(nameof(GetFollowedEvents), async () =>
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
        }, month, year);

    // NOTE: GET /api/v1/locations/{locationId}/follow
    // IMPORTANT: Requires User JWT. Returns whether the current user follows the location.
    // NOTE: Still here for an authoritative answer, but the detail and list payloads now carry isFollowing, so the app rarely needs it.
    [Authorize]
    [HttpGet("{locationId:guid}/follow")]
    public Task<IActionResult> GetFollowStatus(Guid locationId, CancellationToken ct) =>
        ExecuteAsync(nameof(GetFollowStatus), async () =>
        {
            var userId = User.GetUserId();
            var result = await _followService.GetFollowStatusAsync(userId, locationId, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationFollowStatusRetrieved, result);
            return StatusCode(response.Status, response);
        }, locationId);

    // NOTE: POST /api/v1/locations/{locationId}/follow
    // IMPORTANT: Requires User JWT. Idempotent — re-following returns success.
    [Authorize]
    [HttpPost("{locationId:guid}/follow")]
    public Task<IActionResult> FollowLocation(Guid locationId, CancellationToken ct) =>
        ExecuteAsync(nameof(FollowLocation), async () =>
        {
            var userId = User.GetUserId();
            await _followService.FollowLocationAsync(userId, locationId, ct);
            var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationFollowed, null);
            return StatusCode(response.Status, response);
        }, locationId);

    // NOTE: DELETE /api/v1/locations/{locationId}/follow
    // IMPORTANT: Requires User JWT. Idempotent — unfollowing a non-followed location returns success.
    // IMPORTANT: Also cancels the caller's mass reminders at that church - they are not tied to the follow row, and would
    // IMPORTANT:  otherwise keep pushing from a church the user has left.
    [Authorize]
    [HttpDelete("{locationId:guid}/follow")]
    public Task<IActionResult> UnfollowLocation(Guid locationId, CancellationToken ct) =>
        ExecuteAsync(nameof(UnfollowLocation), async () =>
        {
            var userId = User.GetUserId();
            await _followService.UnfollowLocationAsync(userId, locationId, ct);
            var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationUnfollowed, null);
            return StatusCode(response.Status, response);
        }, locationId);
}
