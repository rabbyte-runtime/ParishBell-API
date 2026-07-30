using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.User;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(IUserService userService, IUserDeviceService deviceService, IMessageCache messages) : ControllerBase
{
    private readonly IUserService _userService = userService;
    private readonly IUserDeviceService _deviceService = deviceService;
    private readonly IMessageCache _messages = messages;

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
