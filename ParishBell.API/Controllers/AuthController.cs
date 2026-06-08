using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Auth;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController(IAuthService authService, IMessageCache messages) : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly IMessageCache _messages = messages;

    // NOTE: POST - /api/v1/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken ct)
    {
        var ipAddress = GetClientIpAddress();
        var result = await _authService.RegisterAsync(request, ipAddress, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status201Created, MessageCodes.AuthRegisterSuccess, result);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    // NOTE: POST - /api/v1/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var ipAddress = GetClientIpAddress();
        var result = await _authService.LoginAsync(request, ipAddress, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.AuthLoginSuccess, result);
        return StatusCode(StatusCodes.Status200OK, response);
    }

    // NOTE: POST - /api/v1/auth/refresh-token
    [HttpPost("refresh-token")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken ct)
    {
        var ipAddress = GetClientIpAddress();
        var result = await _authService.RefreshTokenAsync(request, ipAddress, ct);

        var response = ApiResponseBuilder.Build(
            HttpContext,
            _messages,
            StatusCodes.Status200OK,
            MessageCodes.AuthRefreshSuccess,
            result);

        return Ok(response);
    }

    // NOTE: POST - /api/v1/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request, CancellationToken ct)
    {
        await _authService.LogoutAsync(request, ct);

        var response = ApiResponseBuilder.Build<object?>(
            HttpContext,
            _messages,
            StatusCodes.Status200OK,
            MessageCodes.AuthLogoutSuccess,
            null);

        return Ok(response);
    }

    // NOTE: POST - /api/v1/auth/forgot-password
    // IMPORTANT: Always returns 200 with a generic message (silent success)
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken ct)
    {
        var ipAddress = GetClientIpAddress();
        await _authService.ForgotPasswordAsync(request, ipAddress, ct);

        var response = ApiResponseBuilder.Build<object?>(
            HttpContext,
            _messages,
            StatusCodes.Status200OK,
            MessageCodes.AuthForgotPasswordSent,
            null);

        return Ok(response);
    }

    // NOTE: POST - /api/v1/auth/reset-password
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken ct)
    {
        await _authService.ResetPasswordAsync(request, ct);

        var response = ApiResponseBuilder.Build<object?>(
            HttpContext,
            _messages,
            StatusCodes.Status200OK,
            MessageCodes.AuthResetPasswordSuccess,
            null);

        return Ok(response);
    }

    // NOTE: Get client IP address
    private string GetClientIpAddress()
    {
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}