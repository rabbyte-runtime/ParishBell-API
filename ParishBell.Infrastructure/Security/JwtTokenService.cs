using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ParishBell.Core.Configuration;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;

namespace ParishBell.Infrastructure.Security;

public class JwtTokenService(IOptions<JwtSettings> jwt) : IJwtTokenService
{
    private readonly JwtSettings _jwtSettings = jwt.Value;

    public string GenerateAccessToken(AppUser user)
    {
        // NOTE: Create a key from configured secrets
        // IMPORTANT: Secrets are stored in user-secrets store
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

        // NOTE: Define the signing credentials using HMAC SHA-256
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // NOTE: Build the claims that will be embedded in the JWT
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim("full_name",                   user.FullName),
            new Claim("lang",                        user.PreferredLanguage.ToString())
        };

        // NOTE: Create the JWT token object by passing claims and other info
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: AccessTokenExpiresAt(),
            signingCredentials: credentials);

        // NOTE: Serialize the token and return
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRawRefreshToken()
    {
        // NOTE: Generate secure random bytes 
        var bytes = RandomNumberGenerator.GetBytes(64);

        // NOTE: Convert bytes to a Base64 string and return
        // IMPORTANT: + is replaced with -
        // IMPORTANT: / is replaces with
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public string HashToken(string rawToken)
    {
        // NOTE: Convert the raw token string into bytes using UTF-8 encoding
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

        // NOTE: Compute the SHA-256 hash of bytes, hash bytes into a hex string and return
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // NOTE: Add the minutes from JWT settings to UtcNow and return
    public DateTime AccessTokenExpiresAt() => DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes);
}