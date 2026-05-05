using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Auth;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IAuthRepository> _mockRepo;
    private readonly Mock<IPasswordHasher> _mockHasher;
    private readonly Mock<IJwtTokenService> _mockJwt;
    private readonly Mock<IExternalAuthValidator> _mockGoogleValidator;
    private readonly AuthService _authService;

    private readonly Guid _testLanguageId = Guid.NewGuid();

    public AuthServiceTests()
    {
        // NOTE: Create mocks for all dependencies
        _mockRepo = new Mock<IAuthRepository>();
        _mockHasher = new Mock<IPasswordHasher>();
        _mockJwt = new Mock<IJwtTokenService>();

        // NOTE: Mock Google validator
        _mockGoogleValidator = new Mock<IExternalAuthValidator>();
        _mockGoogleValidator.Setup(v => v.Provider).Returns(AuthProvider.Google);

        // NOTE: Create the service with mocked dependencies
        _authService = new AuthService(_mockRepo.Object, _mockHasher.Object, _mockJwt.Object, [_mockGoogleValidator.Object]);

        // NOTE: Set up default JWT mocks
        SetupJwtMocks();
    }

    // NOTE: EMAIL PROVIDER TESTS

    // IMPORTANT: TEST 1 - Valid email registration succeeds
    [Fact]
    public async Task RegisterAsync_Email_WithValidData_ReturnsAuthResponse()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Test User",
            Email = "test@parishbell.lk",
            Password = "Password123",
            ConfirmPassword = "Password123",
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Email
        };

        // NOTE: Mock email does not exist
        _mockRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // NOTE: Mock password hashing
        _mockHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_password");

        // NOTE: Act
        var result = await _authService.RegisterAsync(request, "127.0.0.1");

        // NOTE: Assert
        Assert.NotNull(result);
        Assert.Equal("access_token", result.AccessToken);
        Assert.Equal("refresh_token", result.RefreshToken);
        Assert.NotNull(result.User);
        Assert.Equal("test@parishbell.lk", result.User.Email);
        Assert.Equal("Test User", result.User.FullName);

        // IMPORTANT: Verify CreateUserAsync was called only once with correct provider
        _mockRepo.Verify(r => r.CreateUserAsync(It.Is<AppUser>(u => u.Email == "test@parishbell.lk" && u.FullName == "Test User" && u.AuthProvider == (short)AuthProvider.Email && u.PasswordHash != null), It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: Verify SaveRefreshTokenAsync was called only once
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 2 - Passwords don't match -> BadRequestException
    [Fact]
    public async Task RegisterAsync_Email_PasswordMismatch_ThrowsBadRequestException()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Test User",
            Email = "test@parishbell.lk",
            Password = "Password123",
            ConfirmPassword = "DifferentPassword123",  // IMPORTANT: Mismatch
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Email
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
    public async Task RegisterAsync_Email_WeakPassword_ThrowsBadRequestException(string weakPassword)
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Test User",
            Email = "test@parishbell.lk",
            Password = weakPassword,
            ConfirmPassword = weakPassword,
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Email
        };

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthWeakPassword, exception.MessageCode);

        // NOTE: Verify NO database calls were made
        _mockRepo.Verify(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 4 - Email already exists -> ConflictException
    [Fact]
    public async Task RegisterAsync_Email_AlreadyExists_ThrowsConflictException()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Test User",
            Email = "existing@parishbell.lk",
            Password = "Password123",
            ConfirmPassword = "Password123",
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Email
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
    public async Task RegisterAsync_Email_Normalization_ConvertsToLowercase()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Test User",
            Email = "  MiXedCaSe@ParishBell.LK  ",  // IMPORTANT: Include mixed case + whitespace
            Password = "Password123",
            ConfirmPassword = "Password123",
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Email
        };

        // NOTE: Mock email does not exist
        _mockRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // NOTE: Mock password hashing
        _mockHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");

        // NOTE: Act
        var result = await _authService.RegisterAsync(request, "127.0.0.1");

        // NOTE: Assert - email should be normalized
        Assert.Equal("mixedcase@parishbell.lk", result.User.Email);

        // IMPORTANT: Verify CreateUserAsync was called only once
        _mockRepo.Verify(r => r.CreateUserAsync(It.Is<AppUser>(u => u.Email == "mixedcase@parishbell.lk"), It.IsAny<CancellationToken>()), Times.Once);
    }

    // NOTE: GOOGLE PROVIDER TESTS

    // IMPORTANT: TEST 6 - Valid Google registration succeeds
    [Fact]
    public async Task RegisterAsync_Google_WithValidToken_ReturnsAuthResponse()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            IdToken = "valid_google_id_token",
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Google
        };

        // NOTE: Mock Google validator returns verified user info
        var verifiedInfo = new ExternalAuthResult
        {
            ProviderUserId = "google_user_12345",
            Email = "rabbyte@gmail.com",
            FullName = "Rabbyte",
            EmailVerified = true
        };

        _mockGoogleValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(verifiedInfo);

        // NOTE: Mock no existing Google user with this provider ID
        _mockRepo.Setup(r => r.GetUserByProviderAsync(AuthProvider.Google, "google_user_12345", It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);

        // NOTE: Mock no existing email
        _mockRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // NOTE: Act
        var result = await _authService.RegisterAsync(request, "127.0.0.1");

        // NOTE: Assert
        Assert.NotNull(result);
        Assert.Equal("rabbyte@gmail.com", result.User.Email);

        // IMPORTANT: Verify Google user was created with correct properties
        _mockRepo.Verify(r => r.CreateUserAsync(It.Is<AppUser>(u =>
            u.Email == "rabbyte@gmail.com"
            && u.FullName == "Rabbyte"
            && u.AuthProvider == (short)AuthProvider.Google
            && u.AuthProviderId == "google_user_12345"
            && u.PasswordHash == null  // IMPORTANT: Google users don't have passwords!
        ), It.IsAny<CancellationToken>()), Times.Once);

        // NOTE: Password hasher shouldn't be called for Google users
        _mockHasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    // IMPORTANT: TEST 7 - Missing ID token for Google -> BadRequestException
    [Fact]
    public async Task RegisterAsync_Google_MissingIdToken_ThrowsBadRequestException()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            IdToken = null,  // IMPORTANT: Missing
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Google
        };

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthInvalidSocialToken, exception.MessageCode);

        // IMPORTANT: Verify validator was never called!!
        _mockGoogleValidator.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.CreateUserAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 8 - Email already exists in email/password account -> ConflictException
    [Fact]
    public async Task RegisterAsync_Google_EmailAlreadyRegisteredViaEmail_ThrowsConflictException()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            IdToken = "valid_google_id_token",
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Google
        };

        // NOTE: Google validator returns verified info
        var verifiedInfo = new ExternalAuthResult
        {
            ProviderUserId = "google_user_99999",
            Email = "existing@parishbell.lk",
            FullName = "Existing User",
            EmailVerified = true
        };

        _mockGoogleValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(verifiedInfo);

        // NOTE: No existing Google user with this provider ID
        _mockRepo.Setup(r => r.GetUserByProviderAsync(AuthProvider.Google, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);

        // IMPORTANT: But email already exists (registered via email/password)
        _mockRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(() => _authService.RegisterAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthSocialEmailConflict, exception.MessageCode);

        // IMPORTANT: User must not be created!!
        _mockRepo.Verify(r => r.CreateUserAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 9 - Same Google account already registered -> ConflictException
    [Fact]
    public async Task RegisterAsync_Google_AccountAlreadyRegistered_ThrowsConflictException()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            IdToken = "valid_google_id_token",
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Google
        };

        // NOTE: Google validator returns verified info
        var verifiedInfo = new ExternalAuthResult
        {
            ProviderUserId = "google_user_existing",
            Email = "rabbyte@gmail.com",
            FullName = "Rabbyte",
            EmailVerified = true
        };

        _mockGoogleValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(verifiedInfo);

        // IMPORTANT: This Google account is already registered
        var existingUser = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "rabbyte@gmail.com",
            AuthProvider = (short)AuthProvider.Google,
            AuthProviderId = "google_user_existing"
        };

        _mockRepo.Setup(r => r.GetUserByProviderAsync(AuthProvider.Google, "google_user_existing", It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(() => _authService.RegisterAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthEmailAlreadyExists, exception.MessageCode);

        // IMPORTANT: User must not be created again!!
        _mockRepo.Verify(r => r.CreateUserAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);

        // IMPORTANT: Verify EmailExistsAsync was not called
        _mockRepo.Verify(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 10 - Uses Google's name when request FullName is null
    [Fact]
    public async Task RegisterAsync_Google_NoRequestFullName_UsesGoogleName()
    {
        // NOTE: Arrange - request has no full name
        var request = new RegisterRequestDto
        {
            IdToken = "valid_google_id_token",
            FullName = null,  // IMPORTANT: User did not provide a name
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Google
        };

        // NOTE: Google provides the name
        var verifiedInfo = new ExternalAuthResult
        {
            ProviderUserId = "google_user_xyz",
            Email = "rabbyte@gmail.com",
            FullName = "Rabbyte",  // IMPORTANT: This should be used
            EmailVerified = true
        };

        _mockGoogleValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(verifiedInfo);
        _mockRepo.Setup(r => r.GetUserByProviderAsync(It.IsAny<AuthProvider>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);
        _mockRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // NOTE: Act
        var result = await _authService.RegisterAsync(request, "127.0.0.1");

        // NOTE: Assert - Google's name should be used
        Assert.Equal("Rabbyte", result.User.FullName);

        _mockRepo.Verify(r => r.CreateUserAsync(It.Is<AppUser>(u => u.FullName == "Rabbyte"), It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 11 - Uses request FullName when provided (overrides Google's name)
    [Fact]
    public async Task RegisterAsync_Google_WithRequestFullName_OverridesGoogleName()
    {
        // NOTE: Arrange - request provides a custom name
        var request = new RegisterRequestDto
        {
            IdToken = "valid_google_id_token",
            FullName = "Rabbyte",  // IMPORTANT: This should be taken
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Google
        };

        // NOTE: Google provides a different name
        var verifiedInfo = new ExternalAuthResult
        {
            ProviderUserId = "google_user_xyz",
            Email = "user@gmail.com",
            FullName = "Google Provided Name",  // IMPORTANT: Ignored
            EmailVerified = true
        };

        _mockGoogleValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(verifiedInfo);
        _mockRepo.Setup(r => r.GetUserByProviderAsync(It.IsAny<AuthProvider>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);
        _mockRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // NOTE: Act
        var result = await _authService.RegisterAsync(request, "127.0.0.1");

        // NOTE: Assert - request's name should win
        Assert.Equal("Rabbyte", result.User.FullName);

        _mockRepo.Verify(r => r.CreateUserAsync(It.Is<AppUser>(u => u.FullName == "Rabbyte"), It.IsAny<CancellationToken>()), Times.Once);
    }

    // NOTE: UNSUPPORTED PROVIDER TEST

    // IMPORTANT: TEST 12 - Apple provider not yet implemented -> BadRequestException
    [Fact]
    public async Task RegisterAsync_Apple_NotYetImplemented_ThrowsBadRequestException()
    {
        // NOTE: Arrange
        var request = new RegisterRequestDto
        {
            IdToken = "any_apple_token",
            PreferredLanguage = _testLanguageId,
            Provider = AuthProvider.Apple
        };

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthUnsupportedProvider, exception.MessageCode);

        // IMPORTANT: Verify no downstream calls were made
        _mockRepo.Verify(r => r.CreateUserAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.GetUserByProviderAsync(It.IsAny<AuthProvider>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // NOTE: Helper to set up JWT mocks for tests
    private void SetupJwtMocks()
    {
        _mockJwt.Setup(j => j.GenerateAccessToken(It.IsAny<AppUser>())).Returns("access_token");
        _mockJwt.Setup(j => j.GenerateRawRefreshToken()).Returns("refresh_token");
        _mockJwt.Setup(j => j.HashToken(It.IsAny<string>())).Returns("hashed_refresh");
        _mockJwt.Setup(j => j.AccessTokenExpiresAt()).Returns(DateTime.UtcNow.AddMinutes(15));
        _mockRepo.Setup(r => r.CreateUserAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser u, CancellationToken _) => u);
        _mockRepo.Setup(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }
}