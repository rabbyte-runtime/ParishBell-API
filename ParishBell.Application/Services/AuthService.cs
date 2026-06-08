using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ParishBell.Core.Configuration;
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
    IPasswordResetRepository passwordResetRepository,
    IEmailService emailService,
    IOptions<PasswordResetSettings> passwordResetOptions,
    IMessageCache messageCache,
    IEnumerable<IExternalAuthValidator> externalAuthValidators) : IAuthService
{
    // NOTE: Dependencies for authentication
    private readonly IAuthRepository _authRepository = authRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
    private readonly IPasswordResetRepository _passwordResetRepository = passwordResetRepository;
    private readonly IEmailService _emailService = emailService;
    private readonly PasswordResetSettings _passwordResetSettings = passwordResetOptions.Value;
    private readonly IMessageCache _messageCache = messageCache;

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

    // NOTE: Login flow supporting both email/password and social providers
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string ipAddress, CancellationToken ct = default)
    {
        // NOTE: Conditional logic based on provider
        var user = request.Provider switch
        {
            AuthProvider.Email => await LoginWithEmailAsync(request, ct),
            AuthProvider.Google => await LoginWithSocialAsync(request, AuthProvider.Google, ct),
            AuthProvider.Apple => throw new BadRequestException(MessageCodes.AuthUnsupportedProvider), // TODO: Implement Apple later
            _ => throw new BadRequestException(MessageCodes.AuthUnsupportedProvider)
        };

        // IMPORTANT: Reject inactive accounts
        if (!user.IsActive)
            throw new UnauthorizedException(MessageCodes.AuthAccountInactive);

        // NOTE: Update last login timestamp
        await _authRepository.UpdateLastLoginAsync(user.UserId, ct);

        // NOTE: Issue new tokens
        return await IssueTokensAsync(user, ipAddress, ct);
    }

    // NOTE: Email/password login flow
    private async Task<AppUser> LoginWithEmailAsync(LoginRequestDto request, CancellationToken ct)
    {
        // NOTE: Required fields for email login
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new BadRequestException(MessageCodes.ValidationEmailRequired);

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new BadRequestException(MessageCodes.ValidationPasswordRequired);

        // NOTE: Look up user by email
        var user = await _authRepository.GetUserByEmailAsync(request.Email, ct);

        // IMPORTANT: Generic message - don't reveal whether email exists
        if (user is null)
            throw new UnauthorizedException(MessageCodes.AuthInvalidCredentials);

        // IMPORTANT: User registered with social provider trying email login
        // NOTE: Tell them which provider to use - small security trade-off for better UX
        if (user.AuthProvider != (short)AuthProvider.Email)
            throw new UnauthorizedException(MessageCodes.AuthWrongProvider);

        // IMPORTANT: Verify password using BCrypt
        if (string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException(MessageCodes.AuthInvalidCredentials);

        return user;
    }

    // NOTE: Social login flow (Google for now)
    private async Task<AppUser> LoginWithSocialAsync(LoginRequestDto request, AuthProvider provider, CancellationToken ct)
    {
        // NOTE: ID token is required
        if (string.IsNullOrWhiteSpace(request.IdToken))
            throw new BadRequestException(MessageCodes.AuthInvalidSocialToken);

        var validator = _externalAuthValidators.FirstOrDefault(v => v.Provider == provider)
            ?? throw new BadRequestException(MessageCodes.AuthUnsupportedProvider);

        // NOTE: Validate the ID token (signature, expiry, audience, email_verified)
        var verifiedInfo = await validator.ValidateAsync(request.IdToken, ct);

        // NOTE: Look up the user by social provider + provider user id
        var user = await _authRepository.GetUserByProviderAsync(provider, verifiedInfo.ProviderUserId, ct);

        // IMPORTANT: User exists for this Google account - return it
        if (user is not null)
            return user;

        // NOTE: No social account exists - check if the email is registered with another provider
        var existingByEmail = await _authRepository.GetUserByEmailAsync(verifiedInfo.Email, ct);
        if (existingByEmail is not null)
        {
            // IMPORTANT: Same as register - reject if email is on email/password account
            if (existingByEmail.AuthProvider == (short)AuthProvider.Email)
                throw new ConflictException(MessageCodes.AuthSocialEmailConflict);

            // IMPORTANT: Same email registered with a DIFFERENT social provider - tell user which
            throw new UnauthorizedException(MessageCodes.AuthWrongProvider);
        }

        // IMPORTANT: User has not registered yet - require explicit registration
        throw new UnauthorizedException(MessageCodes.AuthInvalidCredentials);
    }

    // NOTE: Refresh token flow - issues new access + refresh tokens
    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, string ipAddress, CancellationToken ct = default)
    {
        // NOTE: Hash the incoming token to look it up
        var tokenHash = _jwtTokenService.HashToken(request.RefreshToken);
        var storedToken = await _authRepository.GetRefreshTokenByHashAsync(tokenHash, ct) ?? throw new UnauthorizedException(MessageCodes.AuthInvalidRefreshToken);

        // IMPORTANT: Token reuse detection
        // NOTE: A revoked token being presented again likely means it was stolen
        // IMPORTANT: Revoke all tokens for this user as a compromise mitigation
        if (storedToken.IsRevoked)
        {
            await _authRepository.RevokeAllUserRefreshTokensAsync(storedToken.UserId, ct);
            throw new UnauthorizedException(MessageCodes.AuthRefreshTokenReuse);
        }

        // NOTE: Check expiry
        if (storedToken.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedException(MessageCodes.AuthRefreshTokenExpired);

        // NOTE: Verify the user is still active
        var user = storedToken.User
            ?? throw new UnauthorizedException(MessageCodes.AuthInvalidRefreshToken);

        if (!user.IsActive)
            throw new UnauthorizedException(MessageCodes.AuthAccountInactive);

        // IMPORTANT: Rotate - revoke the old token before issuing new one
        await _authRepository.RevokeRefreshTokenAsync(storedToken.RefreshTokenId, ct);

        // NOTE: Update last login timestamp
        await _authRepository.UpdateLastLoginAsync(user.UserId, ct);

        // NOTE: Issue new tokens
        return await IssueTokensAsync(user, ipAddress, ct);
    }

    public async Task LogoutAsync(LogoutRequestDto request, CancellationToken ct = default)
    {
        // NOTE: Hash the token to look it up
        var tokenHash = _jwtTokenService.HashToken(request.RefreshToken);

        var storedToken = await _authRepository.GetRefreshTokenByHashAsync(tokenHash, ct);

        // IMPORTANT: Don't reveal whether token exists
        // NOTE: If token is invalid or already revoked, just return success silently
        if (storedToken is null || storedToken.IsRevoked) return;

        // NOTE: Revoke this single refresh token
        await _authRepository.RevokeRefreshTokenAsync(storedToken.RefreshTokenId, ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request, string ipAddress, CancellationToken ct = default)
    {
        // IMPORTANT: Silent success - same response for ALL cases (no info leak)
        // NOTE: The actual email send only happens for valid email-provider users

        // NOTE: Look up the user
        var user = await _authRepository.GetUserByEmailAsync(request.Email, ct);

        // IMPORTANT: Bail silently if:
        // - Email doesn't exist
        // - User is inactive
        // - User registered with social provider (Google/Apple)
        if (user is null || !user.IsActive || user.AuthProvider != (short)AuthProvider.Email)
        {
            return;
        }

        // IMPORTANT: Rate limit - max N requests per hour per email
        var recentCount = await _passwordResetRepository.CountRecentTokensAsync(user.UserId, TimeSpan.FromHours(1), ct);

        if (recentCount >= _passwordResetSettings.MaxRequestsPerHour)
        {
            // IMPORTANT: Don't reveal the rate limit - silent ignore
            // NOTE: User can try again in an hour
            return;
        }

        // NOTE: Generate a 6-digit code
        var code = GenerateSixDigitCode();

        // NOTE: Hash and store
        var resetToken = new PasswordResetToken
        {
            ResetTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            CodeHash = _jwtTokenService.HashToken(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_passwordResetSettings.CodeExpiryMinutes),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            IsUsed = false
        };

        await _passwordResetRepository.SaveResetTokenAsync(resetToken, ct);

        // NOTE: Get user's preferred language code (en/si/ta) from cache
        var languageCode = await GetUserLanguageCodeAsync(user.PreferredLanguage, ct);

        // NOTE: Send the email (or log in mock mode)
        await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, code, languageCode, ct);
    }

    // NOTE: Generate a cryptographically random 6-digit code
    // IMPORTANT: Uses RandomNumberGenerator (not Random) for unpredictability
    private static string GenerateSixDigitCode()
    {
        // NOTE: 0..999999 range
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(4);
        var number = BitConverter.ToUInt32(bytes, 0) % 1000000;
        return number.ToString("D6"); // NOTE: Pad to 6 digits with leading zeros
    }

    // NOTE: Convert a language UUID to its language code (en/si/ta)
    // TODO: Move to a language cache later for performance
    private static Task<string> GetUserLanguageCodeAsync(Guid languageId, CancellationToken ct)
    {
        // NOTE: For now, default to English. Will be enhanced later with a LanguageCache
        // IMPORTANT: This works because the email template service falls back to English anyway
        return Task.FromResult("en");
    }

    // NOTE: Reset password flow - verifies code, updates password, invalidates tokens
    public async Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken ct = default)
    {
        // NOTE: Validate input
        if (request.NewPassword != request.ConfirmPassword)
            throw new BadRequestException(MessageCodes.AuthPasswordsDoNotMatch);

        if (!PasswordPolicyRegex().IsMatch(request.NewPassword))
            throw new BadRequestException(MessageCodes.AuthWeakPassword);

        // NOTE: Look up the user
        var user = await _authRepository.GetUserByEmailAsync(request.Email, ct);

        // IMPORTANT: Generic error message - don't reveal whether email exists or which provider
        if (user is null || user.AuthProvider != (short)AuthProvider.Email)
            throw new BadRequestException(MessageCodes.AuthInvalidResetCode);

        // NOTE: Verify the code
        var codeHash = _jwtTokenService.HashToken(request.Code);
        var resetToken = await _passwordResetRepository.GetActiveTokenAsync(user.UserId, codeHash, ct);

        if (resetToken is null)
            throw new BadRequestException(MessageCodes.AuthInvalidResetCode);

        // NOTE: Hash the new password and update
        var newPasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _authRepository.UpdateUserPasswordAsync(user.UserId, newPasswordHash, ct);

        // IMPORTANT: Invalidate the used token AND all other active tokens for this user
        await _passwordResetRepository.MarkTokenUsedAsync(resetToken.ResetTokenId, ct);
        await _passwordResetRepository.InvalidateAllActiveTokensAsync(user.UserId, ct);

        // IMPORTANT: Revoke all refresh tokens - force sign-in on all devices for security
        // NOTE: After password change, all existing sessions must be terminated
        await _authRepository.RevokeAllUserRefreshTokensAsync(user.UserId, ct);
    }
}