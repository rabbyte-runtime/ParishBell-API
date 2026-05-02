using ParishBell.Core.Entities;

namespace ParishBell.Core.Interfaces;

public interface IJwtTokenService
{
    // NOTE: Generates an access token for the mobile app users
    string GenerateAccessToken(AppUser user);

    // NOTE: Generates a refresh token
    string GenerateRawRefreshToken();

    // NOTE: Hashes a given token
    string HashToken(string rawToken);

    // NOTE: Get the time of the access token expiry date and time
    DateTime AccessTokenExpiresAt();
}