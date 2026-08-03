using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using ParishBell.Core.Constants;
using ParishBell.Core.Exceptions;

namespace ParishBell.API.Helpers;

public static class ClaimsPrincipalExtensions
{
    // NOTE: Reads the authenticated app user's id from the JWT "sub" claim.
    // NOTE: The default inbound map rewrites "sub" to NameIdentifier, so check both.
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (Guid.TryParse(value, out var userId))
            return userId;

        throw new UnauthorizedException(MessageCodes.GeneralUnauthorized);
    }

    // NOTE: For public endpoints that answer differently when a token happens to be present.
    // NOTE: Null means anonymous rather than a failed read.
    public static Guid? GetUserIdOrNull(this ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return null;

        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
