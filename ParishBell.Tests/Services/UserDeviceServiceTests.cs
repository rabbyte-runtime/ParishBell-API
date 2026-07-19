using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.DTOs.User;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class UserDeviceServiceTests
{
    private readonly Mock<IUserDeviceRepository> _mockRepo;
    private readonly UserDeviceService _service;

    private readonly Guid _userId = Guid.NewGuid();

    public UserDeviceServiceTests()
    {
        _mockRepo = new Mock<IUserDeviceRepository>();
        _service = new UserDeviceService(_mockRepo.Object);
    }

    // IMPORTANT: TEST 1 - Register forwards a trimmed token, numeric platform, and app version to the upsert
    [Fact]
    public async Task RegisterDeviceToken_ForwardsTrimmedValuesAndNumericPlatform()
    {
        // NOTE: Arrange
        var request = new RegisterDeviceTokenRequestDto
        {
            Token = "  fcm_token  ",
            Platform = DevicePlatform.Android,
            AppVersion = "  1.0.0  "
        };

        // NOTE: Act
        await _service.RegisterDeviceTokenAsync(_userId, request);

        // NOTE: Assert — Android maps to short 2, token and version trimmed
        _mockRepo.Verify(r => r.UpsertAsync(_userId, "fcm_token", (short)2, "1.0.0", It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 2 - iOS maps to platform 1
    [Fact]
    public async Task RegisterDeviceToken_iOS_MapsToPlatformOne()
    {
        // NOTE: Arrange
        var request = new RegisterDeviceTokenRequestDto
        {
            Token = "apns_token",
            Platform = DevicePlatform.iOS,
            AppVersion = "2.3.1"
        };

        // NOTE: Act
        await _service.RegisterDeviceTokenAsync(_userId, request);

        // NOTE: Assert
        _mockRepo.Verify(r => r.UpsertAsync(_userId, "apns_token", (short)1, "2.3.1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 3 - Missing/blank app version is normalized to null
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterDeviceToken_BlankAppVersion_PassesNull(string? appVersion)
    {
        // NOTE: Arrange
        var request = new RegisterDeviceTokenRequestDto
        {
            Token = "fcm_token",
            Platform = DevicePlatform.Android,
            AppVersion = appVersion
        };

        // NOTE: Act
        await _service.RegisterDeviceTokenAsync(_userId, request);

        // NOTE: Assert — null, not empty string, reaches the DB
        _mockRepo.Verify(r => r.UpsertAsync(_userId, "fcm_token", (short)2, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 4 - Removal forwards the trimmed token scoped to the caller
    [Fact]
    public async Task RemoveDeviceToken_ForwardsTrimmedTokenScopedToUser()
    {
        // NOTE: Act
        await _service.RemoveDeviceTokenAsync(_userId, "  fcm_token  ");

        // NOTE: Assert
        _mockRepo.Verify(r => r.RemoveByTokenAsync(_userId, "fcm_token", It.IsAny<CancellationToken>()), Times.Once);
    }
}
