using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Auth;
using ParishBell.Core.Entities;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IAuthRepository> _mockRepo;
    private readonly Mock<IPasswordHasher> _mockHasher;
    private readonly Mock<IJwtTokenService> _mockJwt;
    private readonly AuthService _authService;

    private readonly Guid _testLanguageId = Guid.NewGuid();

    public AuthServiceTests()
    {
        // NOTE: Create mocks for all dependencies
        _mockRepo = new Mock<IAuthRepository>();
        _mockHasher = new Mock<IPasswordHasher>();
        _mockJwt = new Mock<IJwtTokenService>();

        // NOTE: Create the service with mocked dependencies
        _authService = new AuthService(_mockRepo.Object, _mockHasher.Object, _mockJwt.Object);
    }

    // IMPORTANT: TEST 1 - Valid registration succeeds
    [Fact]
    public async Task RegisterAsync_WithValidData_ReturnsAuthResponse()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Test User",
            Email = "test@parishbell.lk",
            Password = "Password123",
            ConfirmPassword = "Password123",
            PreferredLanguage = _testLanguageId,
        };

        // NOTE: Mock email does not exist
        _mockRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // NOTE: Mock user creation returns the created user
        _mockRepo.Setup(r => r.CreateUserAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser u, CancellationToken _) => u);

        // NOTE: Mock refresh token save succeeds
        _mockRepo.Setup(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // NOTE: Mock password hashing
        _mockHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_password");

        // NOTE: Mock JWT token generation
        _mockJwt.Setup(j => j.GenerateAccessToken(It.IsAny<AppUser>())).Returns("access_token_123");
        _mockJwt.Setup(j => j.GenerateRawRefreshToken()).Returns("refresh_token_456");
        _mockJwt.Setup(j => j.HashToken(It.IsAny<string>())).Returns("hashed_refresh_token");
        _mockJwt.Setup(j => j.AccessTokenExpiresAt()).Returns(DateTime.UtcNow.AddMinutes(15));

        // NOTE: Act
        var result = await _authService.RegisterAsync(request, "127.0.0.1");

        // NOTE: Assert
        Assert.NotNull(result);
        Assert.Equal("access_token_123", result.AccessToken);
        Assert.Equal("refresh_token_456", result.RefreshToken);
        Assert.NotNull(result.User);
        Assert.Equal("test@parishbell.lk", result.User.Email);
        Assert.Equal("Test User", result.User.FullName);

        // IMPORTANT: Verify CreateUserAsync was called only once
        _mockRepo.Verify(r => r.CreateUserAsync(It.Is<AppUser>(u => u.Email == "test@parishbell.lk" && u.FullName == "Test User"), It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: Verify SaveRefreshTokenAsync was called only once
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 2 - Passwords don't match -> BadRequestException
    [Fact]
    public async Task RegisterAsync_PasswordMismatch_ThrowsBadRequestException()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Test User",
            Email = "test@parishbell.lk",
            Password = "Password123",
            ConfirmPassword = "DifferentPassword123",  // IMPORTANT: Mismatch
            PreferredLanguage = _testLanguageId,
        };

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthPasswordsDoNotMatch, exception.MessageCode);

        // IMPORTANT: Verify no DB calls were made!
        _mockRepo.Verify(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.CreateUserAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 3 - Weak password -> BadRequestException
    [Theory]
    [InlineData("weak")]           // NOTE: Too short, no uppercase, no digit
    [InlineData("password")]       // NOTE: No uppercase, no digit
    [InlineData("PASSWORD")]       // NOTE: No digit
    [InlineData("Pass123")]        // NOTE: Less than 8 characters
    [InlineData("password123")]    // NOTE: No uppercase
    public async Task RegisterAsync_WeakPassword_ThrowsBadRequestException(string weakPassword)
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Test User",
            Email = "test@parishbell.lk",
            Password = weakPassword,
            ConfirmPassword = weakPassword,
            PreferredLanguage = _testLanguageId,
        };

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthWeakPassword, exception.MessageCode);

        // NOTE: Verify NO database calls were made
        _mockRepo.Verify(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 4 - Email already exists -> ConflictException
    [Fact]
    public async Task RegisterAsync_EmailAlreadyExists_ThrowsConflictException()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Test User",
            Email = "existing@parishbell.lk",
            Password = "Password123",
            ConfirmPassword = "Password123",
            PreferredLanguage = _testLanguageId,
        };

        // NOTE: Mock email exists
        _mockRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(() => _authService.RegisterAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthEmailAlreadyExists, exception.MessageCode);

        // NOTE: Verify EmailExistsAsync was called
        _mockRepo.Verify(r => r.EmailExistsAsync("existing@parishbell.lk", It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: Verify user was not created!!
        _mockRepo.Verify(r => r.CreateUserAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 5 - Email normalization
    [Fact]
    public async Task RegisterAsync_EmailNormalization_ConvertsToLowercase()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Test User",
            Email = "  MixedCase@ParishBell.LK  ",  // IMPORTANT: Include mixed case + whitespace
            Password = "Password123",
            ConfirmPassword = "Password123",
            PreferredLanguage = _testLanguageId,
        };

        // NOTE: Mock email exists
        _mockRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // NOTE: Mock user creation returns the created user
        _mockRepo.Setup(r => r.CreateUserAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser u, CancellationToken _) => u);

        // NOTE: Mock refresh token save succeeds
        _mockRepo.Setup(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // NOTE: Mock password hashing
        _mockHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");

        // NOTE: Mock JWT token generation
        _mockJwt.Setup(j => j.GenerateAccessToken(It.IsAny<AppUser>())).Returns("access");
        _mockJwt.Setup(j => j.GenerateRawRefreshToken()).Returns("refresh");
        _mockJwt.Setup(j => j.HashToken(It.IsAny<string>())).Returns("hashed_refresh");
        _mockJwt.Setup(j => j.AccessTokenExpiresAt()).Returns(DateTime.UtcNow.AddMinutes(15));

        // NOTE: Act
        var result = await _authService.RegisterAsync(request, "127.0.0.1");

        // NOTE: Assert - email should be normalized
        Assert.Equal("mixedcase@parishbell.lk", result.User.Email);

        // IMPORTANT: Verify CreateUserAsync was called only once
        _mockRepo.Verify(r => r.CreateUserAsync(It.Is<AppUser>(u => u.Email == "mixedcase@parishbell.lk"), It.IsAny<CancellationToken>()), Times.Once);
    }
}