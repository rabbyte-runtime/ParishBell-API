using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.User;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class UserService(
    IUserRepository userRepository,
    ILanguageRepository languageRepository,
    IAuthRepository authRepository,
    IPasswordHasher passwordHasher,
    IProfilePhotoStorage photoStorage,
    IEnumerable<IExternalAuthValidator> externalAuthValidators) : IUserService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ILanguageRepository _languageRepository = languageRepository;
    private readonly IProfilePhotoStorage _photoStorage = photoStorage;

    // NOTE: Deletion re-checks the sign-up credential, so it needs the auth-side pieces too.
    private readonly IAuthRepository _authRepository = authRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IEnumerable<IExternalAuthValidator> _externalAuthValidators = externalAuthValidators;

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await LoadActiveProfileAsync(userId, ct);
        return await MapToDtoAsync(profile, ct);
    }

    public async Task<UserProfileDto> UpdateProfilePhotoAsync(Guid userId, Stream content, CancellationToken ct = default)
    {
        // IMPORTANT: Loaded first so a deactivated or deleted account cannot push bytes into the container.
        await LoadActiveProfileAsync(userId, ct);

        // NOTE: The blob name is derived from the user id, so this overwrites any previous upload rather than orphaning it.
        var blobName = await _photoStorage.UploadAsync(userId, content, ct);
        await _userRepository.UpdateProfilePhotoBlobAsync(userId, blobName, ct);

        var updated = await LoadActiveProfileAsync(userId, ct);
        return await MapToDtoAsync(updated, ct);
    }

    public async Task<UserProfileDto> RemoveProfilePhotoAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await LoadActiveProfileAsync(userId, ct);

        if (profile.ProfilePhotoBlob is not null)
        {
            // NOTE: Blob first - a failure here leaves the row pointing at a photo that still exists, which is
            //       recoverable. Clearing the column first would strand the blob with nothing referencing it.
            await _photoStorage.DeleteAsync(profile.ProfilePhotoBlob, ct);
            await _userRepository.UpdateProfilePhotoBlobAsync(userId, null, ct);
        }

        // NOTE: Removing the upload falls back to the Google/Apple photo, or to nothing - the app then shows initials.
        var updated = await LoadActiveProfileAsync(userId, ct);
        return await MapToDtoAsync(updated, ct);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken ct = default)
    {
        var current = await LoadActiveProfileAsync(userId, ct);

        var fullName = ResolveFullName(request);
        var email = await ResolveEmailAsync(userId, current, request, ct);
        var preferredLanguage = await ResolvePreferredLanguageAsync(current, request, ct);

        // NOTE: Everything supplied already matches what's stored - skip the write and echo the profile back.
        if (fullName is null && email is null && preferredLanguage is null)
            return await MapToDtoAsync(current, ct);

        await _userRepository.UpdateProfileAsync(userId, fullName, email, preferredLanguage, ct);

        // NOTE: Re-read so the response carries the stored values, including the language names of a new selection.
        var updated = await LoadActiveProfileAsync(userId, ct);
        return await MapToDtoAsync(updated, ct);
    }

    public async Task<NotificationPreferencesDto> GetNotificationPreferencesAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await LoadActiveProfileAsync(userId, ct);
        return MapToPreferencesDto(profile);
    }

    public async Task<NotificationPreferencesDto> UpdateNotificationPreferencesAsync(Guid userId, UpdateNotificationPreferencesRequestDto request, CancellationToken ct = default)
    {
        var current = await LoadActiveProfileAsync(userId, ct);

        // NOTE: Only switches that were supplied *and* differ from what's stored reach the DB.
        var events = Changed(request.Events, current.NotifyEvents);
        var announcements = Changed(request.Announcements, current.NotifyAnnouncements);
        var massReminders = Changed(request.MassReminders, current.NotifyMassReminders);
        var feastDays = Changed(request.FeastDays, current.NotifyFeastDays);

        if (events is null && announcements is null && massReminders is null && feastDays is null)
            return MapToPreferencesDto(current);

        await _userRepository.UpdateNotificationPreferencesAsync(userId, events, announcements, massReminders, feastDays, ct);

        var updated = await LoadActiveProfileAsync(userId, ct);
        return MapToPreferencesDto(updated);
    }

    // NOTE: Null when the switch was left out or already sits where the caller wants it.
    private static bool? Changed(bool? requested, bool stored) =>
        requested.HasValue && requested.Value != stored ? requested : null;

    public async Task DeleteAccountAsync(Guid userId, DeleteAccountRequestDto request, CancellationToken ct = default)
    {
        // NOTE: The entity, not the profile projection - the credential check needs the password hash and provider id.
        var user = await _authRepository.GetUserByIdAsync(userId, ct)
            ?? throw new NotFoundException(MessageCodes.GeneralNotFound);

        if (!user.IsActive)
            throw new UnauthorizedException(MessageCodes.AuthAccountInactive);

        // IMPORTANT: Confirm with the credential this account actually signed up with - an email user cannot delete by presenting a Google token, and vice versa.
        if (request.Provider != (AuthProvider)user.AuthProvider)
            throw new UnauthorizedException(MessageCodes.AuthWrongProvider);

        switch (request.Provider)
        {
            case AuthProvider.Email:
                ConfirmWithPassword(user, request);
                break;
            case AuthProvider.Google:
                await ConfirmWithSocialTokenAsync(user, request, AuthProvider.Google, ct);
                break;
            default:
                // NOTE: Apple sign-in is not implemented yet, so no Apple account can exist to delete.
                throw new BadRequestException(MessageCodes.AuthUnsupportedProvider);
        }

        await _userRepository.DeleteAccountAsync(userId, ct);
    }

    // NOTE: Email accounts confirm with their current password.
    private void ConfirmWithPassword(AppUser user, DeleteAccountRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new BadRequestException(MessageCodes.ValidationPasswordRequired);

        if (string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException(MessageCodes.AuthInvalidCredentials);
    }

    // NOTE: Social accounts confirm with a freshly issued ID token from their provider.
    private async Task ConfirmWithSocialTokenAsync(AppUser user, DeleteAccountRequestDto request, AuthProvider provider, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            throw new BadRequestException(MessageCodes.AuthInvalidSocialToken);

        var validator = _externalAuthValidators.FirstOrDefault(v => v.Provider == provider)
            ?? throw new BadRequestException(MessageCodes.AuthUnsupportedProvider);

        // NOTE: Verifies signature, expiry, audience and email_verified.
        var verifiedInfo = await validator.ValidateAsync(request.IdToken, ct);

        // IMPORTANT: A valid token still has to belong to *this* account - otherwise anyone with a Google account and a stolen access token could delete someone else's.
        if (!string.Equals(verifiedInfo.ProviderUserId, user.AuthProviderId, StringComparison.Ordinal))
            throw new UnauthorizedException(MessageCodes.AuthInvalidCredentials);
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

        // IMPORTANT: Social accounts are keyed to the provider's subject and the email belongs to that provider. Changing it here would silently desync the two, so the change is refused outright.
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

    // NOTE: The client sees one photo field and never learns which source won.
    // NOTE: Precedence: the user's own upload, else the Google/Apple photo, else null - the app then draws initials.
    private async Task<UserProfileDto> MapToDtoAsync(UserProfileResult profile, CancellationToken ct)
    {
        var photoUrl = profile.ProfilePhotoBlob is not null
            // IMPORTANT: Minted per response and short-lived, so it must never be cached or persisted by the client.
            ? await _photoStorage.GetReadUrlAsync(profile.ProfilePhotoBlob, ct)
            : profile.ProfileImageUrl;

        return MapToDto(profile, photoUrl);
    }

    private static UserProfileDto MapToDto(UserProfileResult profile, string? photoUrl) => new()
    {
        UserId = profile.UserId,
        FullName = profile.FullName,
        Email = profile.Email,
        ProfileImageUrl = photoUrl,
        AuthProvider = (AuthProvider)profile.AuthProvider,
        PreferredLanguage = profile.PreferredLanguage,
        PreferredLanguageCode = profile.PreferredLanguageCode,
        PreferredLanguageName = profile.PreferredLanguageName,
        PreferredLanguageNativeName = profile.PreferredLanguageNativeName,
        CreatedAt = FormatUtc(profile.CreatedAt),
        LastLoginAt = profile.LastLoginAt is null ? null : FormatUtc(profile.LastLoginAt.Value)
    };

    private static NotificationPreferencesDto MapToPreferencesDto(UserProfileResult profile) => new()
    {
        Events = profile.NotifyEvents,
        Announcements = profile.NotifyAnnouncements,
        MassReminders = profile.NotifyMassReminders,
        FeastDays = profile.NotifyFeastDays
    };

    // NOTE: DB timestamps are stored as UTC - emit an explicit "Z" so the client parses them unambiguously.
    private static string FormatUtc(DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'");
}
