using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.User;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class UserService(IUserRepository userRepository) : IUserService
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        // NOTE: The token outlives the row it points at - a deleted account still presents a valid JWT until it expires.
        var profile = await _userRepository.GetProfileAsync(userId, ct)
            ?? throw new NotFoundException(MessageCodes.GeneralNotFound);

        // IMPORTANT: Same rule as login — a deactivated account gets 401 so the app signs the user out.
        if (!profile.IsActive)
            throw new UnauthorizedException(MessageCodes.AuthAccountInactive);

        return new UserProfileDto
        {
            UserId = profile.UserId,
            FullName = profile.FullName,
            Email = profile.Email,
            ProfileImageUrl = profile.ProfileImageUrl,
            AuthProvider = (AuthProvider)profile.AuthProvider,
            PreferredLanguage = profile.PreferredLanguage,
            PreferredLanguageCode = profile.PreferredLanguageCode,
            PreferredLanguageName = profile.PreferredLanguageName,
            PreferredLanguageNativeName = profile.PreferredLanguageNativeName,
            CreatedAt = FormatUtc(profile.CreatedAt),
            LastLoginAt = profile.LastLoginAt is null ? null : FormatUtc(profile.LastLoginAt.Value)
        };
    }

    // NOTE: DB timestamps are stored as UTC - emit an explicit "Z" so the client parses them unambiguously.
    private static string FormatUtc(DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'");
}
