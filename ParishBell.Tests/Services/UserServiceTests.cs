using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.User;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockRepo;
    private readonly Mock<ILanguageRepository> _mockLanguageRepo;
    private readonly Mock<IAuthRepository> _mockAuthRepo;
    private readonly Mock<IPasswordHasher> _mockHasher;
    private readonly Mock<IExternalAuthValidator> _mockGoogleValidator;
    private readonly Mock<IProfilePhotoStorage> _mockPhotoStorage;
    private readonly UserService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _languageId = Guid.NewGuid();

    private const string GoogleSubject = "google-sub-12345";

    public UserServiceTests()
    {
        _mockRepo = new Mock<IUserRepository>();
        _mockLanguageRepo = new Mock<ILanguageRepository>();
        _mockAuthRepo = new Mock<IAuthRepository>();
        _mockHasher = new Mock<IPasswordHasher>();

        _mockGoogleValidator = new Mock<IExternalAuthValidator>();
        _mockGoogleValidator.SetupGet(v => v.Provider).Returns(AuthProvider.Google);

        _mockPhotoStorage = new Mock<IProfilePhotoStorage>();

        // NOTE: The SAS URL is minted per response, so the double just returns a recognisable stand-in.
        _mockPhotoStorage
            .Setup(s => s.GetReadUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string blob, CancellationToken _) => $"https://blob/{blob}?sas");

        _service = new UserService(
            _mockRepo.Object,
            _mockLanguageRepo.Object,
            _mockAuthRepo.Object,
            _mockHasher.Object,
            _mockPhotoStorage.Object,
            [_mockGoogleValidator.Object]);
    }

    private UserProfileResult MakeResult(
        short authProvider = 1,
        bool isActive = true,
        string? profileImageUrl = "https://blob/avatar.jpg",
        string? profilePhotoBlob = null,
        DateTime? lastLoginAt = null,
        bool notifyEvents = true,
        bool notifyAnnouncements = true,
        bool notifyMassReminders = true,
        bool notifyFeastDays = true) =>
        new(
            _userId,
            "Rajitha Dassanayake",
            "rajitha@example.com",
            profileImageUrl,
            profilePhotoBlob,
            authProvider,
            isActive,
            new DateTime(2026, 1, 4, 6, 15, 0, DateTimeKind.Utc),
            lastLoginAt,
            _languageId,
            "si",
            "Sinhala",
            "සිංහල",
            notifyEvents,
            notifyAnnouncements,
            notifyMassReminders,
            notifyFeastDays);

    private void SetupRepoReturns(UserProfileResult? result) =>
        _mockRepo
            .Setup(r => r.GetProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    // IMPORTANT: TEST 1 - Every field reaches the client, timestamps as explicit UTC
    [Fact]
    public async Task GetProfile_MapsAllFields()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult(lastLoginAt: new DateTime(2026, 7, 29, 18, 45, 30, DateTimeKind.Utc)));

        // NOTE: Act
        var dto = await _service.GetProfileAsync(_userId);

        // NOTE: Assert
        Assert.Equal(_userId, dto.UserId);
        Assert.Equal("Rajitha Dassanayake", dto.FullName);
        Assert.Equal("rajitha@example.com", dto.Email);
        Assert.Equal("https://blob/avatar.jpg", dto.ProfileImageUrl);
        Assert.Equal(AuthProvider.Email, dto.AuthProvider);
        Assert.Equal(_languageId, dto.PreferredLanguage);
        Assert.Equal("si", dto.PreferredLanguageCode);
        Assert.Equal("Sinhala", dto.PreferredLanguageName);
        Assert.Equal("සිංහල", dto.PreferredLanguageNativeName);
        Assert.Equal("2026-01-04T06:15:00Z", dto.CreatedAt);
        Assert.Equal("2026-07-29T18:45:30Z", dto.LastLoginAt);
    }

    // IMPORTANT: TEST 2 - Numeric provider maps to the client-facing enum
    [Theory]
    [InlineData((short)1, AuthProvider.Email)]
    [InlineData((short)2, AuthProvider.Google)]
    [InlineData((short)3, AuthProvider.Apple)]
    public async Task GetProfile_MapsAuthProvider(short raw, AuthProvider expected)
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult(authProvider: raw));

        // NOTE: Act
        var dto = await _service.GetProfileAsync(_userId);

        // NOTE: Assert
        Assert.Equal(expected, dto.AuthProvider);
    }

    // IMPORTANT: TEST 3 - A user who has never signed in reports no last login
    [Fact]
    public async Task GetProfile_WithoutLastLogin_ReturnsNull()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult(lastLoginAt: null, profileImageUrl: null));

        // NOTE: Act
        var dto = await _service.GetProfileAsync(_userId);

        // NOTE: Assert
        Assert.Null(dto.LastLoginAt);
        Assert.Null(dto.ProfileImageUrl);
    }

    // IMPORTANT: TEST 4 - A valid token whose user row is gone gets 404, not a null profile
    [Fact]
    public async Task GetProfile_WhenUserMissing_ThrowsNotFound()
    {
        // NOTE: Arrange
        SetupRepoReturns(null);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetProfileAsync(_userId));
        Assert.Equal(MessageCodes.GeneralNotFound, exception.MessageCode);
    }

    // IMPORTANT: TEST 5 - A deactivated account gets 401 so the app signs the user out
    [Fact]
    public async Task GetProfile_WhenAccountInactive_ThrowsUnauthorized()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult(isActive: false));

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _service.GetProfileAsync(_userId));
        Assert.Equal(MessageCodes.AuthAccountInactive, exception.MessageCode);
    }

    // NOTE: Nothing was written to the user row.
    private void VerifyNoWrite() =>
        _mockRepo.Verify(r => r.UpdateProfileAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);

    // IMPORTANT: TEST 6 - Only the supplied field is written; the rest arrive as null ("leave alone")
    [Fact]
    public async Task UpdateProfile_WritesOnlySuppliedFields()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult());

        // NOTE: Act — name only, and padded to prove it is trimmed
        await _service.UpdateProfileAsync(_userId, new UpdateProfileRequestDto { FullName = "  Rajitha D.  " });

        // NOTE: Assert
        _mockRepo.Verify(r => r.UpdateProfileAsync(_userId, "Rajitha D.", null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 7 - A whitespace-only name clears nothing; it is rejected
    [Fact]
    public async Task UpdateProfile_BlankFullName_ThrowsBadRequest()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult());

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _service.UpdateProfileAsync(_userId, new UpdateProfileRequestDto { FullName = "   " }));
        Assert.Equal(MessageCodes.ValidationFullNameRequired, exception.MessageCode);
        VerifyNoWrite();
    }

    // IMPORTANT: TEST 8 - An empty request touches nothing and echoes the stored profile back
    [Fact]
    public async Task UpdateProfile_WithNothingToChange_SkipsWrite()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult());

        // NOTE: Act
        var dto = await _service.UpdateProfileAsync(_userId, new UpdateProfileRequestDto());

        // NOTE: Assert
        Assert.Equal("rajitha@example.com", dto.Email);
        VerifyNoWrite();
    }

    // IMPORTANT: TEST 9 - Resubmitting the current values (in any case) is a no-op, not a conflict
    [Fact]
    public async Task UpdateProfile_WithUnchangedValues_SkipsWriteAndUniquenessCheck()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult());

        // NOTE: Act
        await _service.UpdateProfileAsync(_userId, new UpdateProfileRequestDto
        {
            FullName = "Rajitha Dassanayake",
            Email = "RAJITHA@example.com",
            PreferredLanguage = _languageId
        });

        // NOTE: Assert — the name is still sent (harmless rewrite), the unchanged email and language are not
        _mockRepo.Verify(r => r.UpdateProfileAsync(_userId, "Rajitha Dassanayake", null, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.EmailTakenByAnotherUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 10 - A new email is normalized to lowercase before the uniqueness check and the write
    [Fact]
    public async Task UpdateProfile_NewEmail_IsNormalizedAndChecked()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult());
        _mockRepo
            .Setup(r => r.EmailTakenByAnotherUserAsync(_userId, "New.Address@Example.com".ToLowerInvariant(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // NOTE: Act
        await _service.UpdateProfileAsync(_userId, new UpdateProfileRequestDto { Email = "  New.Address@Example.com  " });

        // NOTE: Assert
        _mockRepo.Verify(r => r.EmailTakenByAnotherUserAsync(_userId, "new.address@example.com", It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.UpdateProfileAsync(_userId, null, "new.address@example.com", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 11 - An address already on another account is a conflict, and nothing is written
    [Fact]
    public async Task UpdateProfile_EmailTaken_ThrowsConflict()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult());
        _mockRepo
            .Setup(r => r.EmailTakenByAnotherUserAsync(_userId, "taken@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateProfileAsync(_userId, new UpdateProfileRequestDto { Email = "taken@example.com" }));
        Assert.Equal(MessageCodes.AuthEmailAlreadyExists, exception.MessageCode);
        VerifyNoWrite();
    }

    // IMPORTANT: TEST 12 - Social accounts cannot move their email away from the provider
    [Theory]
    [InlineData((short)2)]
    [InlineData((short)3)]
    public async Task UpdateProfile_EmailOnSocialAccount_ThrowsForbidden(short authProvider)
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult(authProvider: authProvider));

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.UpdateProfileAsync(_userId, new UpdateProfileRequestDto { Email = "new@example.com" }));
        Assert.Equal(MessageCodes.UserEmailChangeNotAllowed, exception.MessageCode);
        VerifyNoWrite();
    }

    // IMPORTANT: TEST 13 - An unknown or retired language is rejected before it can fail as an FK error
    [Fact]
    public async Task UpdateProfile_InactiveLanguage_ThrowsBadRequest()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult());
        var newLanguageId = Guid.NewGuid();
        _mockLanguageRepo
            .Setup(r => r.IsActiveLanguageAsync(newLanguageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _service.UpdateProfileAsync(_userId, new UpdateProfileRequestDto { PreferredLanguage = newLanguageId }));
        Assert.Equal(MessageCodes.ValidationPreferredLanguageInvalid, exception.MessageCode);
        VerifyNoWrite();
    }

    // IMPORTANT: TEST 14 - A valid language change is written and the response reflects the stored row
    [Fact]
    public async Task UpdateProfile_ActiveLanguage_IsWrittenAndReturned()
    {
        // NOTE: Arrange — the re-read after the write returns the updated row
        var newLanguageId = Guid.NewGuid();
        var updated = new UserProfileResult(
            _userId, "Rajitha Dassanayake", "rajitha@example.com", null, null, 1, true,
            new DateTime(2026, 1, 4, 6, 15, 0, DateTimeKind.Utc), null,
            newLanguageId, "ta", "Tamil", "தமிழ்",
            true, true, true, true);

        _mockRepo
            .SetupSequence(r => r.GetProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult())
            .ReturnsAsync(updated);

        _mockLanguageRepo
            .Setup(r => r.IsActiveLanguageAsync(newLanguageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // NOTE: Act
        var dto = await _service.UpdateProfileAsync(_userId, new UpdateProfileRequestDto { PreferredLanguage = newLanguageId });

        // NOTE: Assert
        _mockRepo.Verify(r => r.UpdateProfileAsync(_userId, null, null, newLanguageId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(newLanguageId, dto.PreferredLanguage);
        Assert.Equal("ta", dto.PreferredLanguageCode);
        Assert.Equal("தமிழ்", dto.PreferredLanguageNativeName);
    }

    // IMPORTANT: TEST 15 - A deactivated account cannot edit its profile either
    [Fact]
    public async Task UpdateProfile_WhenAccountInactive_ThrowsUnauthorized()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult(isActive: false));

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.UpdateProfileAsync(_userId, new UpdateProfileRequestDto { FullName = "Rajitha D." }));
        Assert.Equal(MessageCodes.AuthAccountInactive, exception.MessageCode);
        VerifyNoWrite();
    }

    // NOTE: No preference switch reached the DB.
    private void VerifyNoPreferenceWrite() =>
        _mockRepo.Verify(r => r.UpdateNotificationPreferencesAsync(
            It.IsAny<Guid>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()), Times.Never);

    // IMPORTANT: TEST 16 - Stored flags map onto the four client-facing switches
    [Fact]
    public async Task GetNotificationPreferences_MapsAllFourSwitches()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult(notifyEvents: true, notifyAnnouncements: false, notifyMassReminders: true, notifyFeastDays: false));

        // NOTE: Act
        var dto = await _service.GetNotificationPreferencesAsync(_userId);

        // NOTE: Assert
        Assert.True(dto.Events);
        Assert.False(dto.Announcements);
        Assert.True(dto.MassReminders);
        Assert.False(dto.FeastDays);
    }

    // IMPORTANT: TEST 17 - Only the switch that moved is written; the other three arrive as null
    [Fact]
    public async Task UpdateNotificationPreferences_WritesOnlyTheSwitchThatMoved()
    {
        // NOTE: Arrange — everything currently on
        SetupRepoReturns(MakeResult());

        // NOTE: Act
        await _service.UpdateNotificationPreferencesAsync(_userId, new UpdateNotificationPreferencesRequestDto { Announcements = false });

        // NOTE: Assert
        _mockRepo.Verify(r => r.UpdateNotificationPreferencesAsync(_userId, null, false, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 18 - Re-sending values that already match writes nothing
    [Fact]
    public async Task UpdateNotificationPreferences_WithNoActualChange_SkipsWrite()
    {
        // NOTE: Arrange — stored state is on/off/on/off, and the request repeats it
        SetupRepoReturns(MakeResult(notifyEvents: true, notifyAnnouncements: false, notifyMassReminders: true, notifyFeastDays: false));

        // NOTE: Act
        var dto = await _service.UpdateNotificationPreferencesAsync(_userId, new UpdateNotificationPreferencesRequestDto
        {
            Events = true,
            Announcements = false,
            MassReminders = true,
            FeastDays = false
        });

        // NOTE: Assert — the stored set still comes back
        Assert.True(dto.Events);
        Assert.False(dto.Announcements);
        VerifyNoPreferenceWrite();
    }

    // IMPORTANT: TEST 19 - An empty body is a no-op rather than a reset to all-off
    [Fact]
    public async Task UpdateNotificationPreferences_WithEmptyBody_SkipsWrite()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult());

        // NOTE: Act
        var dto = await _service.UpdateNotificationPreferencesAsync(_userId, new UpdateNotificationPreferencesRequestDto());

        // NOTE: Assert
        Assert.True(dto.Events);
        Assert.True(dto.Announcements);
        Assert.True(dto.MassReminders);
        Assert.True(dto.FeastDays);
        VerifyNoPreferenceWrite();
    }

    // IMPORTANT: TEST 20 - Turning several switches off at once writes them all and echoes the stored row
    [Fact]
    public async Task UpdateNotificationPreferences_MultipleSwitches_AreWrittenAndReturned()
    {
        // NOTE: Arrange — the re-read after the write returns the new state
        _mockRepo
            .SetupSequence(r => r.GetProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult())
            .ReturnsAsync(MakeResult(notifyEvents: false, notifyFeastDays: false));

        // NOTE: Act
        var dto = await _service.UpdateNotificationPreferencesAsync(_userId, new UpdateNotificationPreferencesRequestDto
        {
            Events = false,
            FeastDays = false
        });

        // NOTE: Assert
        _mockRepo.Verify(r => r.UpdateNotificationPreferencesAsync(_userId, false, null, null, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(dto.Events);
        Assert.False(dto.FeastDays);
        Assert.True(dto.Announcements);
        Assert.True(dto.MassReminders);
    }

    // IMPORTANT: TEST 21 - A deactivated account cannot read or change its preferences
    [Fact]
    public async Task NotificationPreferences_WhenAccountInactive_ThrowUnauthorized()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult(isActive: false));

        // NOTE: Act & Assert
        var readException = await Assert.ThrowsAsync<UnauthorizedException>(() => _service.GetNotificationPreferencesAsync(_userId));
        Assert.Equal(MessageCodes.AuthAccountInactive, readException.MessageCode);

        var writeException = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.UpdateNotificationPreferencesAsync(_userId, new UpdateNotificationPreferencesRequestDto { Events = false }));
        Assert.Equal(MessageCodes.AuthAccountInactive, writeException.MessageCode);
        VerifyNoPreferenceWrite();
    }

    // NOTE: The stored account the delete request is checked against.
    private void SetupAccount(short authProvider = 1, string? passwordHash = "bcrypt-hash", string? authProviderId = null, bool isActive = true) =>
        _mockAuthRepo
            .Setup(r => r.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppUser
            {
                UserId = _userId,
                FullName = "Rajitha Dassanayake",
                Email = "rajitha@example.com",
                PasswordHash = passwordHash,
                AuthProvider = authProvider,
                AuthProviderId = authProviderId,
                PreferredLanguage = _languageId,
                IsActive = isActive
            });

    private void SetupGoogleTokenResolvesTo(string providerUserId) =>
        _mockGoogleValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalAuthResult
            {
                ProviderUserId = providerUserId,
                Email = "rajitha@example.com",
                FullName = "Rajitha Dassanayake",
                EmailVerified = true
            });

    // NOTE: The account survived.
    private void VerifyNoDelete() =>
        _mockRepo.Verify(r => r.DeleteAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

    // IMPORTANT: TEST 22 - Email account deletes once the current password checks out
    [Fact]
    public async Task DeleteAccount_EmailAccount_WithCorrectPassword_Deletes()
    {
        // NOTE: Arrange
        SetupAccount();
        _mockHasher.Setup(h => h.Verify("Correct1", "bcrypt-hash")).Returns(true);

        // NOTE: Act
        await _service.DeleteAccountAsync(_userId, new DeleteAccountRequestDto
        {
            Provider = AuthProvider.Email,
            Password = "Correct1"
        });

        // NOTE: Assert
        _mockRepo.Verify(r => r.DeleteAccountAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 23 - A wrong password leaves the account standing
    [Fact]
    public async Task DeleteAccount_WrongPassword_ThrowsUnauthorized()
    {
        // NOTE: Arrange
        SetupAccount();
        _mockHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.DeleteAccountAsync(_userId, new DeleteAccountRequestDto
            {
                Provider = AuthProvider.Email,
                Password = "Wrong1234"
            }));
        Assert.Equal(MessageCodes.AuthInvalidCredentials, exception.MessageCode);
        VerifyNoDelete();
    }

    // IMPORTANT: TEST 24 - The JWT alone is not enough; the password field is required
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteAccount_WithoutPassword_ThrowsBadRequest(string? password)
    {
        // NOTE: Arrange
        SetupAccount();

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _service.DeleteAccountAsync(_userId, new DeleteAccountRequestDto
            {
                Provider = AuthProvider.Email,
                Password = password
            }));
        Assert.Equal(MessageCodes.ValidationPasswordRequired, exception.MessageCode);
        VerifyNoDelete();
    }

    // IMPORTANT: TEST 25 - The confirmation must use the provider the account was created with
    [Fact]
    public async Task DeleteAccount_ProviderMismatch_ThrowsUnauthorized()
    {
        // NOTE: Arrange — an email account presenting a Google token
        SetupAccount();

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.DeleteAccountAsync(_userId, new DeleteAccountRequestDto
            {
                Provider = AuthProvider.Google,
                IdToken = "eyJ-token"
            }));
        Assert.Equal(MessageCodes.AuthWrongProvider, exception.MessageCode);
        VerifyNoDelete();
    }

    // IMPORTANT: TEST 26 - Google account deletes when the fresh token resolves to its own subject
    [Fact]
    public async Task DeleteAccount_GoogleAccount_WithMatchingToken_Deletes()
    {
        // NOTE: Arrange
        SetupAccount(authProvider: 2, passwordHash: null, authProviderId: GoogleSubject);
        SetupGoogleTokenResolvesTo(GoogleSubject);

        // NOTE: Act
        await _service.DeleteAccountAsync(_userId, new DeleteAccountRequestDto
        {
            Provider = AuthProvider.Google,
            IdToken = "eyJ-token"
        });

        // NOTE: Assert
        _mockRepo.Verify(r => r.DeleteAccountAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 27 - A valid Google token belonging to someone else cannot delete this account
    [Fact]
    public async Task DeleteAccount_GoogleTokenForAnotherSubject_ThrowsUnauthorized()
    {
        // NOTE: Arrange
        SetupAccount(authProvider: 2, passwordHash: null, authProviderId: GoogleSubject);
        SetupGoogleTokenResolvesTo("google-sub-somebody-else");

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.DeleteAccountAsync(_userId, new DeleteAccountRequestDto
            {
                Provider = AuthProvider.Google,
                IdToken = "eyJ-token"
            }));
        Assert.Equal(MessageCodes.AuthInvalidCredentials, exception.MessageCode);
        VerifyNoDelete();
    }

    // IMPORTANT: TEST 28 - A Google account must supply a token, and it is never validated when blank
    [Fact]
    public async Task DeleteAccount_GoogleAccount_WithoutIdToken_ThrowsBadRequest()
    {
        // NOTE: Arrange
        SetupAccount(authProvider: 2, passwordHash: null, authProviderId: GoogleSubject);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _service.DeleteAccountAsync(_userId, new DeleteAccountRequestDto { Provider = AuthProvider.Google }));
        Assert.Equal(MessageCodes.AuthInvalidSocialToken, exception.MessageCode);
        _mockGoogleValidator.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoDelete();
    }

    // IMPORTANT: TEST 29 - Apple has no validator yet, so it is refused rather than silently deleting
    [Fact]
    public async Task DeleteAccount_AppleAccount_ThrowsBadRequest()
    {
        // NOTE: Arrange
        SetupAccount(authProvider: 3, passwordHash: null, authProviderId: "apple-sub");

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _service.DeleteAccountAsync(_userId, new DeleteAccountRequestDto
            {
                Provider = AuthProvider.Apple,
                IdToken = "eyJ-token"
            }));
        Assert.Equal(MessageCodes.AuthUnsupportedProvider, exception.MessageCode);
        VerifyNoDelete();
    }

    // IMPORTANT: TEST 30 - Deleting an account that is already gone is a 404, not a second delete
    [Fact]
    public async Task DeleteAccount_WhenUserMissing_ThrowsNotFound()
    {
        // NOTE: Arrange
        _mockAuthRepo
            .Setup(r => r.GetUserByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeleteAccountAsync(_userId, new DeleteAccountRequestDto
            {
                Provider = AuthProvider.Email,
                Password = "Correct1"
            }));
        Assert.Equal(MessageCodes.GeneralNotFound, exception.MessageCode);
        VerifyNoDelete();
    }

    // IMPORTANT: TEST 31 - A deactivated account is rejected before the credential is even checked
    [Fact]
    public async Task DeleteAccount_WhenAccountInactive_ThrowsUnauthorized()
    {
        // NOTE: Arrange
        SetupAccount(isActive: false);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.DeleteAccountAsync(_userId, new DeleteAccountRequestDto
            {
                Provider = AuthProvider.Email,
                Password = "Correct1"
            }));
        Assert.Equal(MessageCodes.AuthAccountInactive, exception.MessageCode);
        _mockHasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        VerifyNoDelete();
    }

    // IMPORTANT: TEST 32 - An uploaded photo wins over the provider one, and goes out as a minted SAS URL
    [Fact]
    public async Task GetProfile_WithUploadedPhoto_ReturnsSasUrlNotProviderUrl()
    {
        // NOTE: Arrange — the user has both a Google photo and an upload of their own
        _mockRepo
            .Setup(r => r.GetProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(
                authProvider: 2,
                profileImageUrl: "https://lh3.googleusercontent.com/a/abc123",
                profilePhotoBlob: $"{_userId}.jpg"));

        // NOTE: Act
        var result = await _service.GetProfileAsync(_userId);

        // NOTE: Assert
        Assert.Equal($"https://blob/{_userId}.jpg?sas", result.ProfileImageUrl);
        _mockPhotoStorage.Verify(s => s.GetReadUrlAsync($"{_userId}.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 33 - With no upload, the Google photo is handed back untouched - it is already a URL
    [Fact]
    public async Task GetProfile_WithoutUpload_FallsBackToProviderPhoto()
    {
        // NOTE: Arrange
        _mockRepo
            .Setup(r => r.GetProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(
                authProvider: 2,
                profileImageUrl: "https://lh3.googleusercontent.com/a/abc123",
                profilePhotoBlob: null));

        // NOTE: Act
        var result = await _service.GetProfileAsync(_userId);

        // NOTE: Assert
        Assert.Equal("https://lh3.googleusercontent.com/a/abc123", result.ProfileImageUrl);
        _mockPhotoStorage.Verify(s => s.GetReadUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 34 - An email user with neither gets null, and the app draws initials
    [Fact]
    public async Task GetProfile_WithNoPhotoAtAll_ReturnsNull()
    {
        // NOTE: Arrange
        _mockRepo
            .Setup(r => r.GetProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(profileImageUrl: null, profilePhotoBlob: null));

        // NOTE: Act
        var result = await _service.GetProfileAsync(_userId);

        // NOTE: Assert
        Assert.Null(result.ProfileImageUrl);
    }

    // IMPORTANT: TEST 35 - Uploading stores the blob, points the row at it, and answers with the new URL
    [Fact]
    public async Task UpdateProfilePhoto_StoresBlobAndPointsRowAtIt()
    {
        // NOTE: Arrange — the re-read after the write returns a row carrying the upload
        var blobName = $"{_userId}.jpg";
        _mockRepo
            .SetupSequence(r => r.GetProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult())
            .ReturnsAsync(MakeResult(profilePhotoBlob: blobName));

        _mockPhotoStorage
            .Setup(s => s.UploadAsync(_userId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blobName);

        // NOTE: Act
        using var content = new MemoryStream([1, 2, 3]);
        var result = await _service.UpdateProfilePhotoAsync(_userId, content);

        // NOTE: Assert
        _mockPhotoStorage.Verify(s => s.UploadAsync(_userId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.UpdateProfilePhotoBlobAsync(_userId, blobName, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal($"https://blob/{blobName}?sas", result.ProfileImageUrl);
    }

    // IMPORTANT: TEST 36 - A deactivated account cannot push bytes into the container
    [Fact]
    public async Task UpdateProfilePhoto_WhenAccountInactive_ThrowsBeforeUploading()
    {
        // NOTE: Arrange
        _mockRepo
            .Setup(r => r.GetProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(isActive: false));

        // NOTE: Act & Assert
        using var content = new MemoryStream([1, 2, 3]);
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _service.UpdateProfilePhotoAsync(_userId, content));
        Assert.Equal(MessageCodes.AuthAccountInactive, exception.MessageCode);
        _mockPhotoStorage.Verify(s => s.UploadAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 37 - Removing deletes the blob, clears the column, and falls back to the provider photo
    [Fact]
    public async Task RemoveProfilePhoto_DeletesBlobAndFallsBackToProviderPhoto()
    {
        // NOTE: Arrange
        var blobName = $"{_userId}.jpg";
        _mockRepo
            .SetupSequence(r => r.GetProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(authProvider: 2, profileImageUrl: "https://lh3.googleusercontent.com/a/abc123", profilePhotoBlob: blobName))
            .ReturnsAsync(MakeResult(authProvider: 2, profileImageUrl: "https://lh3.googleusercontent.com/a/abc123", profilePhotoBlob: null));

        // NOTE: Act
        var result = await _service.RemoveProfilePhotoAsync(_userId);

        // NOTE: Assert
        _mockPhotoStorage.Verify(s => s.DeleteAsync(blobName, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.UpdateProfilePhotoBlobAsync(_userId, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("https://lh3.googleusercontent.com/a/abc123", result.ProfileImageUrl);
    }

    // IMPORTANT: TEST 38 - Removing one that was never set touches neither the container nor the row
    [Fact]
    public async Task RemoveProfilePhoto_WhenNoneUploaded_IsANoOp()
    {
        // NOTE: Arrange
        _mockRepo
            .Setup(r => r.GetProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(profilePhotoBlob: null));

        // NOTE: Act
        var result = await _service.RemoveProfilePhotoAsync(_userId);

        // NOTE: Assert
        _mockPhotoStorage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.UpdateProfilePhotoBlobAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(result);
    }
}
