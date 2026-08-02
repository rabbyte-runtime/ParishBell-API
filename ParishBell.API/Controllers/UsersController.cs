using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.User;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(IUserService userService, IUserNotificationService notificationService, IUserDeviceService deviceService, IMassReminderService reminderService, IMessageCache messages) : ControllerBase
{
    private readonly IUserService _userService = userService;
    private readonly IUserNotificationService _notificationService = notificationService;
    private readonly IUserDeviceService _deviceService = deviceService;
    private readonly IMassReminderService _reminderService = reminderService;
    private readonly IMessageCache _messages = messages;

    // NOTE: Mirrors BlobStorageSettings.MaxUploadBytes - must be a constant for the RequestSizeLimit attribute.
    private const int MaxPhotoBytes = 12 * 1024 * 1024;

    // NOTE: GET /api/v1/users/me
    // IMPORTANT: Requires User JWT. The profile returned is always the token's own user - the caller cannot ask for another.
    // NOTE: 404 when the account no longer exists, 401 when it has been deactivated - both mean the app should sign out.
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _userService.GetProfileAsync(userId, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.UserProfileRetrieved, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: PUT /api/v1/users/me
    // IMPORTANT: Requires User JWT. Edits the token's own user - the caller cannot update another.
    // NOTE: Partial update - fields left out (or null) keep their stored value. Returns the saved profile so the app can rebind.
    // NOTE: 403 when a Google/Apple user tries to change their email, 409 when the address is taken, 400 for an unknown language.
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequestDto request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _userService.UpdateProfileAsync(userId, request, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.UserProfileUpdated, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: PUT /api/v1/users/me/photo
    // IMPORTANT: Requires User JWT. Multipart form upload under the field name "photo". Replaces whatever the user had.
    // NOTE: Open to every provider - a Google or Apple user may override their account photo with one of their own.
    // NOTE: The image is cropped square and re-encoded server-side, so the stored photo is ours, not the uploaded bytes.
    // NOTE: Returns the profile with a freshly minted photo URL. 422 when the file is missing, oversized, or not a decodable image.
    [HttpPut("me/photo")]
    [RequestSizeLimit(MaxPhotoBytes)]
    public async Task<IActionResult> UpdatePhoto(IFormFile? photo, CancellationToken ct)
    {
        if (photo is null || photo.Length == 0)
            throw new UnprocessableException(MessageCodes.ValidationPhotoRequired);

        // NOTE: Checked here as well as by RequestSizeLimit, which rejects at the pipeline with no coded message.
        if (photo.Length > MaxPhotoBytes)
            throw new UnprocessableException(MessageCodes.ValidationPhotoTooLarge);

        var userId = User.GetUserId();

        await using var stream = photo.OpenReadStream();
        var result = await _userService.UpdateProfilePhotoAsync(userId, stream, ct);

        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.UserProfilePhotoUpdated, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: DELETE /api/v1/users/me/photo
    // IMPORTANT: Requires User JWT. Removes the blob and clears the column, so the Google/Apple photo takes over again - or nothing does, and the app draws initials.
    // NOTE: Idempotent - a user who never uploaded one still gets a 200 and their profile back.
    [HttpDelete("me/photo")]
    public async Task<IActionResult> RemovePhoto(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _userService.RemoveProfilePhotoAsync(userId, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.UserProfilePhotoRemoved, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: GET /api/v1/users/me/notifications
    // IMPORTANT: Requires User JWT. The signed-in user's inbox, newest first. Only delivered notifications appear.
    // NOTE: page defaults to 1, pageSize to 20 and is capped at 100. hasMore drives the lazy-load.
    // NOTE: Each item carries the deep-link ids for its type - locationId, eventId, announcementId, calendarId - null where they do not apply.
    [HttpGet("me/notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _notificationService.GetNotificationsAsync(userId, page, pageSize, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationsRetrieved, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: GET /api/v1/users/me/notifications/unread-count
    // IMPORTANT: Requires User JWT. Counts the caller's own unread notifications only.
    // NOTE: Counts exactly what the inbox list would show, so the badge and the list always agree. An empty inbox is 0, not a 404.
    [HttpGet("me/notifications/unread-count")]
    public async Task<IActionResult> GetUnreadNotificationCount(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _notificationService.GetUnreadCountAsync(userId, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationUnreadCountRetrieved, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: PUT /api/v1/users/me/notifications/{notificationId}/read
    // IMPORTANT: Requires User JWT. Scoped to the caller - another user's notification id returns 404, not 403.
    // NOTE: Idempotent - marking an already-read notification succeeds.
    [HttpPut("me/notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid notificationId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _notificationService.MarkReadAsync(userId, notificationId, ct);
        var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationMarkedRead, null);
        return StatusCode(response.Status, response);
    }

    // NOTE: POST /api/v1/users/me/notifications/read-all
    // IMPORTANT: Requires User JWT. Clears the unread badge in one call. Idempotent - an empty inbox succeeds.
    [HttpPost("me/notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _notificationService.MarkAllReadAsync(userId, ct);
        var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationsAllMarkedRead, null);
        return StatusCode(response.Status, response);
    }

    // NOTE: GET /api/v1/users/me/reminders
    // IMPORTANT: Requires User JWT. Every mass reminder the caller has set, in one place - the calendar only shows them a month at a time.
    // NOTE: A weekly agenda, ordered by dayOfWeek (0=Sunday) then massTime. No paging - a user has a handful of these.
    // NOTE: Cancelled reminders are included as isActive:false so they can be revived; POST /api/v1/mass/reminders with the scheduleId does that.
    // NOTE: isFollowing flags reminders at churches the caller has since unfollowed - those still fire, and this is the only screen that surfaces them.
    // NOTE: Reminders whose mass or church has been hidden are left out entirely; they can never fire again.
    [HttpGet("me/reminders")]
    public async Task<IActionResult> GetMassReminders(
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        CancellationToken ct)
    {
        var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
        var userId = User.GetUserId();
        var result = await _reminderService.GetRemindersAsync(userId, languageCode, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.MassRemindersRetrieved, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: GET /api/v1/users/me/notification-preferences
    // IMPORTANT: Requires User JWT. Four push opt-ins, all on by default.
    // NOTE: Stored only - the push pipeline does not consult them yet, so the toggles are decorative for now.
    [HttpGet("me/notification-preferences")]
    public async Task<IActionResult> GetNotificationPreferences(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _userService.GetNotificationPreferencesAsync(userId, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationPreferencesRetrieved, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: PUT /api/v1/users/me/notification-preferences
    // IMPORTANT: Requires User JWT. Partial update - send only the switches that moved; the rest keep their stored value.
    // NOTE: Returns the full stored set so the settings screen can rebind from the response.
    [HttpPut("me/notification-preferences")]
    public async Task<IActionResult> UpdateNotificationPreferences([FromBody] UpdateNotificationPreferencesRequestDto request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _userService.UpdateNotificationPreferencesAsync(userId, request, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationPreferencesUpdated, result);
        return StatusCode(response.Status, response);
    }

    // NOTE: DELETE /api/v1/users/me
    // IMPORTANT: Requires User JWT *and* the account's own credential - password for Email accounts, a fresh ID token for Google.
    // IMPORTANT: Permanent. The user row and everything hanging off it (devices, follows, reminders, tokens, notification log) is removed.
    // NOTE: Rate limited like the auth endpoints - it verifies a password, so it is a brute-force target.
    // NOTE: 401 on a wrong credential or the wrong provider, 400 when the confirmation field for that provider is missing.
    [EnableRateLimiting("auth")]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe([FromBody] DeleteAccountRequestDto request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _userService.DeleteAccountAsync(userId, request, ct);
        var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.UserAccountDeleted, null);
        return StatusCode(response.Status, response);
    }

    // NOTE: PUT /api/v1/users/me/device-token
    // IMPORTANT: Requires User JWT. Idempotent upsert keyed by the globally-unique device token - re-registering the same token (even from another account) re-points it to the caller.
    [HttpPut("me/device-token")]
    public async Task<IActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequestDto request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _deviceService.RegisterDeviceTokenAsync(userId, request, ct);
        var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.DeviceTokenRegistered, null);
        return StatusCode(response.Status, response);
    }

    // NOTE: DELETE /api/v1/users/me/device-token
    // IMPORTANT: Requires User JWT. Idempotent - removing an unknown token (or one owned by another user) is a no-op.
    [HttpDelete("me/device-token")]
    public async Task<IActionResult> RemoveDeviceToken([FromBody] RemoveDeviceTokenRequestDto request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _deviceService.RemoveDeviceTokenAsync(userId, request.Token, ct);
        var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.DeviceTokenRemoved, null);
        return StatusCode(response.Status, response);
    }
}
