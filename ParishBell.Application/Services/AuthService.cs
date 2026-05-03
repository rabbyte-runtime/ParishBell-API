using System.Text.RegularExpressions;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Auth;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public partial class AuthService(IAuthRepository authRepository, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService) : IAuthService
{
    // NOTE: Dependencies for authentication
    private readonly IAuthRepository _authRepository = authRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;

    // IMPORTANT: Regex password policy - ≥8 chars, at least 1 uppercase and 1 digit
    [GeneratedRegex(@"^(?=.*[A-Z])(?=.*\d).{8,}$")]
    private static partial Regex PasswordPolicyRegex();

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, string ipAddress, CancellationToken ct = default)
    {
        // NOTE: Check if passwords match
        if (request.Password != request.ConfirmPassword)
            throw new BadRequestException(MessageCodes.AuthPasswordsDoNotMatch);

        // IMPORTANT: Password policy - min 8 chars, 1 uppercase, 1 digit
        if (!PasswordPolicyRegex().IsMatch(request.Password))
            throw new BadRequestException(MessageCodes.AuthWeakPassword);

        // NOTE: Check email uniqueness
        if (await _authRepository.EmailExistsAsync(request.Email, ct))
            throw new ConflictException(MessageCodes.AuthEmailAlreadyExists);

        // NOTE: Add new app user
        var user = new AppUser
        {
            UserId = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            AuthProvider = (short)AuthProvider.Email,
            AuthProviderId = null,
            PreferredLanguage = request.PreferredLanguage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _authRepository.CreateUserAsync(user, ct);

        // NOTE: Issue tokens
        var rawRefreshToken = _jwtTokenService.GenerateRawRefreshToken();
        var expiresAt = _jwtTokenService.AccessTokenExpiresAt();

        // NOTE: Add a new refresh token
        var refreshToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = _jwtTokenService.HashToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            IsRevoked = false
        };

        await _authRepository.SaveRefreshTokenAsync(refreshToken, ct);

        // NOTE: Return response
        return new AuthResponseDto
        {
            AccessToken = _jwtTokenService.GenerateAccessToken(user),
            RefreshToken = rawRefreshToken,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                PreferredLanguage = user.PreferredLanguage
            },
        };
    }
}