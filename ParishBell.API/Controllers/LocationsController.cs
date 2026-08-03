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

    // NOTE: Seven days inclusive of today - the week the profile mass tab shows.
    private const int DefaultMassWindowDays = 6;

    // NOTE: Room for a month view without letting a caller ask for years of expanded occurrences.
    private const int MaxMassWindowDays = 62;

    // NOTE: GET /api/v1/locations/{locationId}
    // IMPORTANT: Public - no JWT required.
    // NOTE: isFollowing is folded in when a token is present, so this is one call.
    // NOTE: Anonymous callers get isFollowing:false - not signed in, not "not following".
    // NOTE: massSchedules are dated occurrences, the same shape /mass/schedule returns.
    // NOTE: The client never projects weekdays onto dates itself.
    // NOTE: Optional ?massFrom= and ?massTo= pick the window, defaulting to the coming week.
    // NOTE: Capped at 62 days; an inverted range is a 400.
    // NOTE: Occurrences carry the caller reminder, so profile bells match the calendar.
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

            // NOTE: An unqualified call returns this week, which is what the mass tab wants.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var resolvedFrom = massFrom ?? today;
            var resolvedTo = massTo ?? resolvedFrom.AddDays(DefaultMassWindowDays);

            // IMPORTANT: Expansion is linear in the window, so an unbounded range fans out.
            if (resolvedTo < resolvedFrom || resolvedFrom.AddDays(MaxMassWindowDays) < resolvedTo)
                throw new BadRequestException(MessageCodes.MassScheduleInvalidRange);

            var result = await _locationService.GetLocationByIdAsync(locationId, languageCode, resolvedFrom, resolvedTo, User.GetUserIdOrNull(), ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LocationDetailRetrieved, result);
            return StatusCode(response.Status, response);
        }, locationId, massFrom, massTo);

    // NOTE: GET /api/v1/locations/{locationId}/events
    // IMPORTANT: Requires User JWT.
    // NOTE: Optional fromDate/toDate filters. Ordered ascending by event date.
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
    // IMPORTANT: Requires User JWT. Joined-members only - 403 when not following.
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
    // NOTE: Optional ?q= searches name and address.
    // NOTE: Optional bbox params limit results to the visible viewport.
    // NOTE: userLat and userLng order by nearest-first and populate distanceKm.
    // NOTE: page and pageSize (default 100) lazy-load the list.
    // NOTE: isFollowing is one batched query, so no follow lookup per card.
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
    // IMPORTANT: Requires User JWT. The user follows, most recently followed first.
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
    // NOTE: Optional ?month= and ?year= filter to one month, defaulting to the current.
    // NOTE: A flat list by date then start time, each tagged with its church.
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
    // NOTE: Still here for an authoritative answer, but the payloads now carry isFollowing.
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
    // IMPORTANT: Also cancels the caller mass reminders at that church.
    // NOTE: They are not tied to the follow row and would keep pushing otherwise.
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
