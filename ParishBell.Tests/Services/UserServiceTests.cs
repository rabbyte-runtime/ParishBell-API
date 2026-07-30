using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.User;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockRepo;
    private readonly Mock<ILanguageRepository> _mockLanguageRepo;
    private readonly UserService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _languageId = Guid.NewGuid();

    public UserServiceTests()
    {
        _mockRepo = new Mock<IUserRepository>();
        _mockLanguageRepo = new Mock<ILanguageRepository>();
        _service = new UserService(_mockRepo.Object, _mockLanguageRepo.Object);
    }

    private UserProfileResult MakeResult(
        short authProvider = 1,
        bool isActive = true,
        string? profileImageUrl = "https://blob/avatar.jpg",
        DateTime? lastLoginAt = null) =>
        new(
            _userId,
            "Rajitha Dassanayake",
            "rajitha@example.com",
            profileImageUrl,
            authProvider,
            isActive,
            new DateTime(2026, 1, 4, 6, 15, 0, DateTimeKind.Utc),
            lastLoginAt,
            _languageId,
            "si",
            "Sinhala",
            "සිංහල");

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
            _userId, "Rajitha Dassanayake", "rajitha@example.com", null, 1, true,
            new DateTime(2026, 1, 4, 6, 15, 0, DateTimeKind.Utc), null,
            newLanguageId, "ta", "Tamil", "தமிழ்");

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
}
