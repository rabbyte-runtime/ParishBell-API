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
public class UsersController(
    IUserService userService,
    IUserNotificationService notificationService,
    IUserDeviceService deviceService,
    IMassReminderService reminderService,
    IMessageCache messages,
    ILogger<UsersController> logger) : ApiControllerBase(logger)
{
    private readonly IUserService _userService = userService;
    private readonly IUserNotificationService _notificationService = notificationService;
    private readonly IUserDeviceService _deviceService = deviceService;
    private readonly IMassReminderService _reminderService = reminderService;
    private readonly IMessageCache _messages = messages;

    // NOTE: Mirrors BlobStorageSettings.MaxUploadBytes - RequestSizeLimit needs a constant.
    private const int MaxPhotoBytes = 12 * 1024 * 1024;

    // NOTE: GET /api/v1/users/me
    // IMPORTANT: Requires User JWT. Always the token own user, never another.
    // NOTE: 404 when the account is gone, 401 when deactivated - both mean sign out.
    [HttpGet("me")]
    public Task<IActionResult> GetMe(CancellationToken ct) =>
        ExecuteAsync(nameof(GetMe), async () =>
        {
            var userId = User.GetUserId();
            var result = await _userService.GetProfileAsync(userId, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.UserProfileRetrieved, result);
            return StatusCode(response.Status, response);
        });

    // NOTE: PUT /api/v1/users/me
    // IMPORTANT: Requires User JWT. Edits the token's own user - the caller cannot update another.
    // NOTE: Partial update - omitted fields keep their stored value.
    // NOTE: Returns the saved profile so the app can rebind.
    // NOTE: 403 on a social email change, 409 when taken, 400 for an unknown language.
    [HttpPut("me")]
    public Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(UpdateMe), async () =>
        {
            var userId = User.GetUserId();
            var result = await _userService.UpdateProfileAsync(userId, request, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.UserProfileUpdated, result);
            return StatusCode(response.Status, response);
        });

    // NOTE: PUT /api/v1/users/me/photo
    // IMPORTANT: Requires User JWT. Multipart upload under the field name "photo".
    // NOTE: Open to every provider - a social user may override their account photo.
    // NOTE: Cropped square and re-encoded server-side, so the stored photo is ours.
    // NOTE: Rate limited per user - each upload costs a decode and a blob write.
    // NOTE: Returns the profile with a freshly minted photo URL.
    // NOTE: 422 when missing or oversized, 400 when not a decodable image.
    [HttpPut("me/photo")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(MaxPhotoBytes)]
    public Task<IActionResult> UpdatePhoto(IFormFile? photo, CancellationToken ct) =>
        ExecuteAsync(nameof(UpdatePhoto), async () =>
        {
            if (photo is null || photo.Length == 0)
                throw new UnprocessableException(MessageCodes.ValidationPhotoRequired);

            // NOTE: Also checked by RequestSizeLimit, which rejects with no coded message.
            if (photo.Length > MaxPhotoBytes)
                throw new UnprocessableException(MessageCodes.ValidationPhotoTooLarge);

            var userId = User.GetUserId();

            await using var stream = photo.OpenReadStream();
            var result = await _userService.UpdateProfilePhotoAsync(userId, stream, ct);

            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.UserProfilePhotoUpdated, result);
            return StatusCode(response.Status, response);
        }, photo?.Length, photo?.ContentType);

    // NOTE: DELETE /api/v1/users/me/photo
    // IMPORTANT: Requires User JWT. Removes the blob and clears the column.
    // NOTE: The provider photo takes over, or the app draws initials.
    // NOTE: Idempotent - a user who never uploaded one still gets a 200 and their profile back.
    [HttpDelete("me/photo")]
    public Task<IActionResult> RemovePhoto(CancellationToken ct) =>
        ExecuteAsync(nameof(RemovePhoto), async () =>
        {
            var userId = User.GetUserId();
            var result = await _userService.RemoveProfilePhotoAsync(userId, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.UserProfilePhotoRemoved, result);
            return StatusCode(response.Status, response);
        });

    // NOTE: GET /api/v1/users/me/notifications
    // IMPORTANT: Requires User JWT. The inbox newest first, delivered rows only.
    // NOTE: page defaults to 1, pageSize to 20 and is capped at 100. hasMore drives the lazy-load.
    // NOTE: Each item carries the deep-link ids for its type, null where they do not apply.
    [HttpGet("me/notifications")]
    public Task<IActionResult> GetNotifications([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        ExecuteAsync(nameof(GetNotifications), async () =>
        {
            var userId = User.GetUserId();
            var result = await _notificationService.GetNotificationsAsync(userId, page, pageSize, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationsRetrieved, result);
            return StatusCode(response.Status, response);
        }, page, pageSize);

    // NOTE: GET /api/v1/users/me/notifications/unread-count
    // IMPORTANT: Requires User JWT. Counts the caller's own unread notifications only.
    // NOTE: Counts exactly what the list shows, so badge and list always agree.
    // NOTE: An empty inbox is 0, not a 404.
    [HttpGet("me/notifications/unread-count")]
    public Task<IActionResult> GetUnreadNotificationCount(CancellationToken ct) =>
        ExecuteAsync(nameof(GetUnreadNotificationCount), async () =>
        {
            var userId = User.GetUserId();
            var result = await _notificationService.GetUnreadCountAsync(userId, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationUnreadCountRetrieved, result);
            return StatusCode(response.Status, response);
        });

    // NOTE: PUT /api/v1/users/me/notifications/{notificationId}/read
    // IMPORTANT: Requires User JWT. Another user notification id returns 404, not 403.
    // NOTE: Idempotent - marking an already-read notification succeeds.
    [HttpPut("me/notifications/{notificationId:guid}/read")]
    public Task<IActionResult> MarkNotificationRead(Guid notificationId, CancellationToken ct) =>
        ExecuteAsync(nameof(MarkNotificationRead), async () =>
        {
            var userId = User.GetUserId();
            await _notificationService.MarkReadAsync(userId, notificationId, ct);
            var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationMarkedRead, null);
            return StatusCode(response.Status, response);
        }, notificationId);

    // NOTE: POST /api/v1/users/me/notifications/read-all
    // IMPORTANT: Requires User JWT. Clears the badge in one idempotent call.
    [HttpPost("me/notifications/read-all")]
    public Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct) =>
        ExecuteAsync(nameof(MarkAllNotificationsRead), async () =>
        {
            var userId = User.GetUserId();
            await _notificationService.MarkAllReadAsync(userId, ct);
            var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationsAllMarkedRead, null);
            return StatusCode(response.Status, response);
        });

    // NOTE: GET /api/v1/users/me/reminders
    // IMPORTANT: Requires User JWT. Every reminder the caller has set, in one place.
    // NOTE: The calendar only ever shows them a month at a time.
    // NOTE: A weekly agenda by dayOfWeek then massTime. No paging - there are few.
    // NOTE: Cancelled reminders come back as isActive:false so they can be revived.
    // NOTE: isFollowing flags churches the caller left. Unfollowing cancels them too.
    // NOTE: Those rows are historical rather than live.
    // NOTE: Reminders on a hidden mass or church are left out - they can never fire.
    [HttpGet("me/reminders")]
    public Task<IActionResult> GetMassReminders(
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        CancellationToken ct) =>
        ExecuteAsync(nameof(GetMassReminders), async () =>
        {
            var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
            var userId = User.GetUserId();
            var result = await _reminderService.GetRemindersAsync(userId, languageCode, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.MassRemindersRetrieved, result);
            return StatusCode(response.Status, response);
        });

    // NOTE: GET /api/v1/users/me/notification-preferences
    // IMPORTANT: Requires User JWT. Four push opt-ins, all on by default.
    // NOTE: The mass reminder switch is honoured by the push job; the rest are stored only.
    [HttpGet("me/notification-preferences")]
    public Task<IActionResult> GetNotificationPreferences(CancellationToken ct) =>
        ExecuteAsync(nameof(GetNotificationPreferences), async () =>
        {
            var userId = User.GetUserId();
            var result = await _userService.GetNotificationPreferencesAsync(userId, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationPreferencesRetrieved, result);
            return StatusCode(response.Status, response);
        });

    // NOTE: PUT /api/v1/users/me/notification-preferences
    // IMPORTANT: Requires User JWT. Send only the switches that moved.
    // NOTE: Returns the full stored set so the settings screen can rebind from the response.
    [HttpPut("me/notification-preferences")]
    public Task<IActionResult> UpdateNotificationPreferences([FromBody] UpdateNotificationPreferencesRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(UpdateNotificationPreferences), async () =>
        {
            var userId = User.GetUserId();
            var result = await _userService.UpdateNotificationPreferencesAsync(userId, request, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.NotificationPreferencesUpdated, result);
            return StatusCode(response.Status, response);
        });

    // NOTE: DELETE /api/v1/users/me
    // IMPORTANT: Requires User JWT and the account own credential.
    // NOTE: Password for Email accounts, a fresh ID token for Google.
    // IMPORTANT: Permanent. The user row and every child row is removed.
    // NOTE: The profile photo blob goes with it.
    // NOTE: Rate limited like auth - it verifies a password, so it is a brute-force target.
    // NOTE: 401 on a wrong credential or provider, 400 when the confirmation is missing.
    [EnableRateLimiting("auth")]
    [HttpDelete("me")]
    public Task<IActionResult> DeleteMe([FromBody] DeleteAccountRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(DeleteMe), async () =>
        {
            var userId = User.GetUserId();
            await _userService.DeleteAccountAsync(userId, request, ct);
            var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.UserAccountDeleted, null);
            return StatusCode(response.Status, response);
        }, request.Provider);

    // NOTE: PUT /api/v1/users/me/device-token
    // IMPORTANT: Requires User JWT. Idempotent upsert keyed by the device token.
    // NOTE: Re-registering a token from another account re-points it to the caller.
    [HttpPut("me/device-token")]
    public Task<IActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(RegisterDeviceToken), async () =>
        {
            var userId = User.GetUserId();
            await _deviceService.RegisterDeviceTokenAsync(userId, request, ct);
            var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.DeviceTokenRegistered, null);
            return StatusCode(response.Status, response);
        }, request.Platform);

    // NOTE: DELETE /api/v1/users/me/device-token
    // IMPORTANT: Requires User JWT. Removing an unknown or foreign token is a no-op.
    [HttpDelete("me/device-token")]
    public Task<IActionResult> RemoveDeviceToken([FromBody] RemoveDeviceTokenRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(RemoveDeviceToken), async () =>
        {
            var userId = User.GetUserId();
            await _deviceService.RemoveDeviceTokenAsync(userId, request.Token, ct);
            var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.DeviceTokenRemoved, null);
            return StatusCode(response.Status, response);
        });
}
