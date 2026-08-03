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
public class AuthController(IAuthService authService, IMessageCache messages, ILogger<AuthController> logger) : ApiControllerBase(logger)
{
    private readonly IAuthService _authService = authService;
    private readonly IMessageCache _messages = messages;

    // NOTE: POST - /api/v1/auth/register
    // IMPORTANT: Nothing here logs the request body - it carries passwords and ID tokens.
    [HttpPost("register")]
    public Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(Register), async () =>
        {
            var ipAddress = GetClientIpAddress();
            var result = await _authService.RegisterAsync(request, ipAddress, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status201Created, MessageCodes.AuthRegisterSuccess, result);
            return StatusCode(StatusCodes.Status201Created, response);
        }, request.Provider);

    // NOTE: POST - /api/v1/auth/login
    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(Login), async () =>
        {
            var ipAddress = GetClientIpAddress();
            var result = await _authService.LoginAsync(request, ipAddress, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.AuthLoginSuccess, result);
            return StatusCode(StatusCodes.Status200OK, response);
        }, request.Provider);

    // NOTE: POST - /api/v1/auth/refresh-token
    [HttpPost("refresh-token")]
    public Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(Refresh), async () =>
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
        });

    // NOTE: POST - /api/v1/auth/logout
    [HttpPost("logout")]
    public Task<IActionResult> Logout([FromBody] LogoutRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(Logout), async () =>
        {
            await _authService.LogoutAsync(request, ct);

            var response = ApiResponseBuilder.Build<object?>(
                HttpContext,
                _messages,
                StatusCodes.Status200OK,
                MessageCodes.AuthLogoutSuccess,
                null);

            return Ok(response);
        });

    // NOTE: POST - /api/v1/auth/forgot-password
    // IMPORTANT: Always returns 200 with a generic message (silent success)
    [HttpPost("forgot-password")]
    public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(ForgotPassword), async () =>
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
        });

    // NOTE: POST - /api/v1/auth/reset-password
    [HttpPost("reset-password")]
    public Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(ResetPassword), async () =>
        {
            await _authService.ResetPasswordAsync(request, ct);

            var response = ApiResponseBuilder.Build<object?>(
                HttpContext,
                _messages,
                StatusCodes.Status200OK,
                MessageCodes.AuthResetPasswordSuccess,
                null);

            return Ok(response);
        });

    // NOTE: Get client IP address
    private string GetClientIpAddress()
    {
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
