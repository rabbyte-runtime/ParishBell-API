using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.User;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class UserService(IUserRepository userRepository, ILanguageRepository languageRepository) : IUserService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ILanguageRepository _languageRepository = languageRepository;

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await LoadActiveProfileAsync(userId, ct);
        return MapToDto(profile);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken ct = default)
    {
        var current = await LoadActiveProfileAsync(userId, ct);

        var fullName = ResolveFullName(request);
        var email = await ResolveEmailAsync(userId, current, request, ct);
        var preferredLanguage = await ResolvePreferredLanguageAsync(current, request, ct);

        // NOTE: Everything supplied already matches what's stored - skip the write and echo the profile back.
        if (fullName is null && email is null && preferredLanguage is null)
            return MapToDto(current);

        await _userRepository.UpdateProfileAsync(userId, fullName, email, preferredLanguage, ct);

        // NOTE: Re-read so the response carries the stored values, including the language names of a new selection.
        var updated = await LoadActiveProfileAsync(userId, ct);
        return MapToDto(updated);
    }

    // NOTE: Null when the name was left out or already matches; trimmed value otherwise.
    private static string? ResolveFullName(UpdateProfileRequestDto request)
    {
        if (request.FullName is null) return null;

        var fullName = request.FullName.Trim();

        // IMPORTANT: A whitespace-only name passes the length annotation but is not a name.
        if (fullName.Length == 0)
            throw new BadRequestException(MessageCodes.ValidationFullNameRequired);

        return fullName;
    }

    // NOTE: Null when the email was left out or unchanged; normalized (lowercased) value otherwise.
    private async Task<string?> ResolveEmailAsync(Guid userId, UserProfileResult current, UpdateProfileRequestDto request, CancellationToken ct)
    {
        if (request.Email is null) return null;

        // NOTE: Stored lowercase at registration — normalize before comparing or writing.
        var email = request.Email.Trim().ToLowerInvariant();

        if (email == current.Email) return null;

        // IMPORTANT: Social accounts are keyed to the provider's subject and the email belongs to that provider.
        //            Changing it here would silently desync the two, so the change is refused outright.
        if (current.AuthProvider != (short)AuthProvider.Email)
            throw new ForbiddenException(MessageCodes.UserEmailChangeNotAllowed);

        if (await _userRepository.EmailTakenByAnotherUserAsync(userId, email, ct))
            throw new ConflictException(MessageCodes.AuthEmailAlreadyExists);

        return email;
    }

    // NOTE: Null when the language was left out or unchanged; the validated id otherwise.
    private async Task<Guid?> ResolvePreferredLanguageAsync(UserProfileResult current, UpdateProfileRequestDto request, CancellationToken ct)
    {
        if (!request.PreferredLanguage.HasValue) return null;

        var languageId = request.PreferredLanguage.Value;

        if (languageId == current.PreferredLanguage) return null;

        // IMPORTANT: Reject unknown or retired languages here — otherwise the FK fails as an unexplained 500.
        if (!await _languageRepository.IsActiveLanguageAsync(languageId, ct))
            throw new BadRequestException(MessageCodes.ValidationPreferredLanguageInvalid);

        return languageId;
    }

    // NOTE: The token outlives the row it points at - a deleted account still presents a valid JWT until it expires.
    private async Task<UserProfileResult> LoadActiveProfileAsync(Guid userId, CancellationToken ct)
    {
        var profile = await _userRepository.GetProfileAsync(userId, ct)
            ?? throw new NotFoundException(MessageCodes.GeneralNotFound);

        // IMPORTANT: Same rule as login — a deactivated account gets 401 so the app signs the user out.
        if (!profile.IsActive)
            throw new UnauthorizedException(MessageCodes.AuthAccountInactive);

        return profile;
    }

    private static UserProfileDto MapToDto(UserProfileResult profile) => new()
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

    // NOTE: DB timestamps are stored as UTC - emit an explicit "Z" so the client parses them unambiguously.
    private static string FormatUtc(DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'");
}
