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
public class UsersController(IUserDeviceService deviceService, IMessageCache messages) : ControllerBase
{
    private readonly IUserDeviceService _deviceService = deviceService;
    private readonly IMessageCache _messages = messages;

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
