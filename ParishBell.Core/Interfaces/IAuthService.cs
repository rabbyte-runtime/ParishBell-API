using ParishBell.Core.DTOs.Auth;

namespace ParishBell.Core.Interfaces;

public interface IAuthService
{
    // NOTE: Registers a new mobile app user
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, string ipAddress, CancellationToken ct = default);

    // NOTE: Logins a user using email/password or social provider
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string ipAddress, CancellationToken ct = default);

    // NOTE: Issues new access + refresh tokens using a valid refresh token
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, string ipAddress, CancellationToken ct = default);

    // NOTE: Logs out a user by revoking the provided refresh token
    Task LogoutAsync(LogoutRequestDto request, CancellationToken ct = default);

    // NOTE: Initiate password reset - sends code via email (always returns 200, silent success)
    Task ForgotPasswordAsync(ForgotPasswordRequestDto request, string ipAddress, CancellationToken ct = default);

    // NOTE: Verify code and update password
    Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken ct = default);
}