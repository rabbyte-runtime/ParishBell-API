using System.Text.RegularExpressions;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Auth;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public partial class AuthService(
    IAuthRepository authRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IEnumerable<IExternalAuthValidator> externalAuthValidators) : IAuthService
{
    // NOTE: Dependencies for authentication
    private readonly IAuthRepository _authRepository = authRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;

    // NOTE: All registered external auth validators (Google only as of now)
    private readonly IEnumerable<IExternalAuthValidator> _externalAuthValidators = externalAuthValidators;

    // IMPORTANT: Regex password policy - ≥8 chars, at least 1 uppercase and 1 digit
    [GeneratedRegex(@"^(?=.*[A-Z])(?=.*\d).{8,}$")]
    private static partial Regex PasswordPolicyRegex();

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, string ipAddress, CancellationToken ct = default)
    {
        // NOTE: Conditional logic based on provider
        var user = request.Provider switch
        {
            AuthProvider.Email => await RegisterWithEmailAsync(request, ct),
            AuthProvider.Google => await RegisterWithSocialAsync(request, AuthProvider.Google, ct),
            AuthProvider.Apple => throw new BadRequestException(MessageCodes.AuthUnsupportedProvider), // TODO: Implement Apple later
            _ => throw new BadRequestException(MessageCodes.AuthUnsupportedProvider)
        };

        // NOTE: Issue tokens (same flow for all providers)
        return await IssueTokensAsync(user, ipAddress, ct);
    }

    // NOTE: Email registration flow
    private async Task<AppUser> RegisterWithEmailAsync(RegisterRequestDto request, CancellationToken ct)
    {
        // NOTE: Required fields for email registration
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new BadRequestException(MessageCodes.ValidationFullNameRequired);

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new BadRequestException(MessageCodes.ValidationEmailRequired);

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new BadRequestException(MessageCodes.ValidationPasswordRequired);

        if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
            throw new BadRequestException(MessageCodes.ValidationConfirmPasswordRequired);

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
        return user;
    }

    // NOTE: Social registration flow (Google for now)
    private async Task<AppUser> RegisterWithSocialAsync(RegisterRequestDto request, AuthProvider provider, CancellationToken ct)
    {
        // NOTE: ID token is required for social providers
        if (string.IsNullOrWhiteSpace(request.IdToken))
            throw new BadRequestException(MessageCodes.AuthInvalidSocialToken);

        // NOTE: Find the validator for this provider
        var validator = _externalAuthValidators.FirstOrDefault(v => v.Provider == provider)
            ?? throw new BadRequestException(MessageCodes.AuthUnsupportedProvider);

        // NOTE: Validate the ID token - this verifies signature, expiry, audience, email_verified
        var verifiedInfo = await validator.ValidateAsync(request.IdToken, ct);

        // IMPORTANT: Check if this social account was already registered
        var existingSocialUser = await _authRepository.GetUserByProviderAsync(provider, verifiedInfo.ProviderUserId, ct);
        if (existingSocialUser is not null)
            throw new ConflictException(MessageCodes.AuthEmailAlreadyExists);

        // IMPORTANT: Reject if email already registered with another provider
        // NOTE: User must sign in with their original provider, not create duplicate accounts
        if (await _authRepository.EmailExistsAsync(verifiedInfo.Email, ct))
            throw new ConflictException(MessageCodes.AuthSocialEmailConflict);

        // NOTE: Determine full name - use request value if provided, else fallback to provider's name
        var fullName = !string.IsNullOrWhiteSpace(request.FullName)
            ? request.FullName.Trim()
            : verifiedInfo.FullName.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
            throw new BadRequestException(MessageCodes.ValidationFullNameRequired);

        // NOTE: Add new social app user
        var user = new AppUser
        {
            UserId = Guid.NewGuid(),
            FullName = fullName,
            Email = verifiedInfo.Email, // NOTE: Already lowercased by the validator
            PasswordHash = null, // IMPORTANT: Social users have no passwords
            AuthProvider = (short)provider,
            AuthProviderId = verifiedInfo.ProviderUserId,
            PreferredLanguage = request.PreferredLanguage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _authRepository.CreateUserAsync(user, ct);
        return user;
    }

    // NOTE: Issue access & refresh tokens
    private async Task<AuthResponseDto> IssueTokensAsync(AppUser user, string ipAddress, CancellationToken ct)
    {
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