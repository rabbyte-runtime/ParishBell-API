using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using ParishBell.Core.Configuration;
using ParishBell.Core.Constants;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Infrastructure.Security;

public class GoogleAuthValidator(IOptions<GoogleAuthSettings> options) : IExternalAuthValidator
{
    private readonly GoogleAuthSettings _settings = options.Value;

    // NOTE: This validator handles Google
    public AuthProvider Provider => AuthProvider.Google;

    public async Task<ExternalAuthResult> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        // NOTE: Validate using Google's official library
        // IMPORTANT: This verifies the signature, expiry, issuer, and audience
        GoogleJsonWebSignature.Payload payload;

        try
        {
            var validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                // NOTE: Token must be issued to one of our configured client IDs
                Audience = _settings.ClientIds,
            };

            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);
        }
        catch (InvalidJwtException)
        {
            // NOTE: Invalid signature, expired, wrong audience, etc.
            throw new UnauthorizedException(MessageCodes.AuthInvalidSocialToken);
        }

        // IMPORTANT: Reject Google accounts where email is not verified
        if (!payload.EmailVerified)
            throw new UnauthorizedException(MessageCodes.AuthGoogleEmailNotVerified);

        // NOTE: Return the verified user info
        return new ExternalAuthResult
        {
            ProviderUserId = payload.Subject,          // NOTE: Google's unique user ID (sub claim)
            Email = payload.Email.ToLowerInvariant(),
            FullName = payload.Name ?? string.Empty,   // NOTE: Google's display name
            EmailVerified = payload.EmailVerified
        };
    }
}
