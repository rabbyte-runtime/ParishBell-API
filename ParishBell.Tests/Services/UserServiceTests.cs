using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockRepo;
    private readonly UserService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _languageId = Guid.NewGuid();

    public UserServiceTests()
    {
        _mockRepo = new Mock<IUserRepository>();
        _service = new UserService(_mockRepo.Object);
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
}
