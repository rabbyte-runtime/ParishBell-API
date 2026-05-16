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

    // NOTE: REGISTER TESTS

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

    // NOTE: LOGIN TESTS

    // IMPORTANT: TEST 13 - Valid email login succeeds
    [Fact]
    public async Task LoginAsync_Email_WithValidCredentials_ReturnsAuthResponse()
    {
        // NOTE: Arrange
        var request = new LoginRequestDto
        {
            Email = "test@parishbell.lk",
            Password = "Password123",
            Provider = AuthProvider.Email
        };

        // NOTE: Mock user exists with email provider
        var existingUser = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "test@parishbell.lk",
            FullName = "Test User",
            PasswordHash = "hashed_password",
            AuthProvider = (short)AuthProvider.Email,
            IsActive = true,
            PreferredLanguage = _testLanguageId
        };

        _mockRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);

        // NOTE: Mock password verification succeeds
        _mockHasher.Setup(h => h.Verify("Password123", "hashed_password")).Returns(true);

        // NOTE: Mock last login update
        _mockRepo.Setup(r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // NOTE: Act
        var result = await _authService.LoginAsync(request, "127.0.0.1");

        // NOTE: Assert
        Assert.NotNull(result);
        Assert.Equal("access_token", result.AccessToken);
        Assert.Equal("refresh_token", result.RefreshToken);
        Assert.Equal("test@parishbell.lk", result.User.Email);

        // IMPORTANT: Verify password verify was called with correct args
        _mockHasher.Verify(h => h.Verify("Password123", "hashed_password"), Times.Once);

        // IMPORTANT: Verify LastLoginAt was updated
        _mockRepo.Verify(r => r.UpdateLastLoginAsync(existingUser.UserId, It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: Verify a new refresh token was saved
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 14 - Wrong password -> UnauthorizedException
    [Fact]
    public async Task LoginAsync_Email_WithWrongPassword_ThrowsUnauthorizedException()
    {
        // NOTE: Arrange
        var request = new LoginRequestDto
        {
            Email = "test@parishbell.lk",
            Password = "WrongPassword",
            Provider = AuthProvider.Email
        };

        var existingUser = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "test@parishbell.lk",
            PasswordHash = "hashed_password",
            AuthProvider = (short)AuthProvider.Email,
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);

        // IMPORTANT: Password verification fails
        _mockHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthInvalidCredentials, exception.MessageCode);

        // IMPORTANT: No tokens issued, no last-login update
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 15 - Non-existent email -> UnauthorizedException (same as wrong password - security)
    [Fact]
    public async Task LoginAsync_Email_WithNonExistentEmail_ThrowsUnauthorizedException()
    {
        // NOTE: Arrange
        var request = new LoginRequestDto
        {
            Email = "nonexistent@parishbell.lk",
            Password = "Password123",
            Provider = AuthProvider.Email
        };

        // IMPORTANT: User not found
        _mockRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthInvalidCredentials, exception.MessageCode);

        // IMPORTANT: Password verify must never be called - prevents timing attacks revealing email existence
        _mockHasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        // IMPORTANT: No tokens issued
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 16 - Email login on Google-registered account -> UnauthorizedException
    [Fact]
    public async Task LoginAsync_Email_OnGoogleAccount_ThrowsWrongProviderException()
    {
        // NOTE: Arrange
        var request = new LoginRequestDto
        {
            Email = "rabbyte@gmail.com",
            Password = "Password123",
            Provider = AuthProvider.Email
        };

        // IMPORTANT: User exists but registered via Google
        var googleUser = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "rabbyte@gmail.com",
            PasswordHash = null, // NOTE: Google users have no password
            AuthProvider = (short)AuthProvider.Google,
            AuthProviderId = "google_user_id",
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(googleUser);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthWrongProvider, exception.MessageCode);

        // IMPORTANT: Password verify must NEVER be called
        _mockHasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        // IMPORTANT: No tokens issued
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 17 - Valid Google login (existing user) succeeds
    [Fact]
    public async Task LoginAsync_Google_WithExistingUser_ReturnsAuthResponse()
    {
        // NOTE: Arrange
        var request = new LoginRequestDto
        {
            IdToken = "valid_google_id_token",
            Provider = AuthProvider.Google
        };

        // NOTE: Google validator returns verified info
        var verifiedInfo = new ExternalAuthResult
        {
            ProviderUserId = "google_user_12345",
            Email = "rabbyte@gmail.com",
            FullName = "Rabbyte",
            EmailVerified = true
        };

        _mockGoogleValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(verifiedInfo);

        // NOTE: User exists in DB for this Google account
        var existingUser = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "rabbyte@gmail.com",
            FullName = "Rabbyte",
            AuthProvider = (short)AuthProvider.Google,
            AuthProviderId = "google_user_12345",
            PasswordHash = null,
            IsActive = true,
            PreferredLanguage = _testLanguageId
        };

        _mockRepo.Setup(r => r.GetUserByProviderAsync(AuthProvider.Google, "google_user_12345", It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);

        // NOTE: Mock last login update
        _mockRepo.Setup(r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // NOTE: Act
        var result = await _authService.LoginAsync(request, "127.0.0.1");

        // NOTE: Assert
        Assert.NotNull(result);
        Assert.Equal("rabbyte@gmail.com", result.User.Email);

        // IMPORTANT: Email lookup must NOT happen - we found user by provider ID already
        _mockRepo.Verify(r => r.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // IMPORTANT: LastLoginAt updated
        _mockRepo.Verify(r => r.UpdateLastLoginAsync(existingUser.UserId, It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: Refresh token saved
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 18 - Google login on email/password account -> ConflictException
    [Fact]
    public async Task LoginAsync_Google_OnEmailAccount_ThrowsConflictException()
    {
        // NOTE: Arrange
        var request = new LoginRequestDto
        {
            IdToken = "valid_google_id_token",
            Provider = AuthProvider.Google
        };

        // NOTE: Google validator returns verified info
        var verifiedInfo = new ExternalAuthResult
        {
            ProviderUserId = "google_user_99999",
            Email = "registered@parishbell.lk",
            FullName = "Registered User",
            EmailVerified = true
        };

        _mockGoogleValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(verifiedInfo);

        // NOTE: No user found for this Google account
        _mockRepo.Setup(r => r.GetUserByProviderAsync(AuthProvider.Google, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);

        // IMPORTANT: But email exists, registered with Email provider
        var emailUser = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "registered@parishbell.lk",
            AuthProvider = (short)AuthProvider.Email,
            PasswordHash = "hashed",
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(emailUser);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(() => _authService.LoginAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthSocialEmailConflict, exception.MessageCode);

        // IMPORTANT: No tokens issued
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 19 - Google login with no account at all -> UnauthorizedException
    [Fact]
    public async Task LoginAsync_Google_NoAccountExists_ThrowsUnauthorizedException()
    {
        // NOTE: Arrange
        var request = new LoginRequestDto
        {
            IdToken = "valid_google_id_token",
            Provider = AuthProvider.Google
        };

        // NOTE: Google validator returns verified info
        var verifiedInfo = new ExternalAuthResult
        {
            ProviderUserId = "google_user_brand_new",
            Email = "brandnew@gmail.com",
            FullName = "Brand New User",
            EmailVerified = true
        };

        _mockGoogleValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(verifiedInfo);

        // IMPORTANT: No user found for this Google account
        _mockRepo.Setup(r => r.GetUserByProviderAsync(It.IsAny<AuthProvider>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);

        // IMPORTANT: No user found by email either
        _mockRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AppUser?)null);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthInvalidCredentials, exception.MessageCode);

        // IMPORTANT: No tokens issued - user must register first
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 20 - Inactive user tries to login -> UnauthorizedException
    [Fact]
    public async Task LoginAsync_InactiveUser_ThrowsUnauthorizedException()
    {
        // NOTE: Arrange
        var request = new LoginRequestDto
        {
            Email = "inactive@parishbell.lk",
            Password = "Password123",
            Provider = AuthProvider.Email
        };

        // IMPORTANT: User found but inactive
        var inactiveUser = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "inactive@parishbell.lk",
            PasswordHash = "hashed_password",
            AuthProvider = (short)AuthProvider.Email,
            IsActive = false  // IMPORTANT: Account is deactivated
        };

        _mockRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(inactiveUser);

        // NOTE: Password verification succeeds - we want to test the IsActive check, not the password
        _mockHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthAccountInactive, exception.MessageCode);

        // IMPORTANT: LastLoginAt must NOT update for inactive accounts
        _mockRepo.Verify(r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        // IMPORTANT: No tokens issued
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // NOTE: REFRESH TOKEN TESTS

    // IMPORTANT: TEST 21 - Valid refresh token succeeds, old token revoked
    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_ReturnsNewTokensAndRevokesOld()
    {
        // NOTE: Arrange
        var request = new RefreshTokenRequestDto
        {
            RefreshToken = "raw_refresh_token"
        };

        // NOTE: Active user attached to the refresh token
        var user = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "test@parishbell.lk",
            FullName = "Test User",
            IsActive = true,
            PreferredLanguage = _testLanguageId,
            AuthProvider = (short)AuthProvider.Email
        };

        // NOTE: A valid, unrevoked, unexpired refresh token in DB
        var storedToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            TokenHash = "hashed_refresh", // IMPORTANT: Matches what _mockJwt.HashToken returns
            ExpiresAt = DateTime.UtcNow.AddDays(15),
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            IsRevoked = false
        };

        _mockRepo.Setup(r => r.GetRefreshTokenByHashAsync("hashed_refresh", It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);

        // NOTE: Mock revocation
        _mockRepo.Setup(r => r.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // NOTE: Mock last login update
        _mockRepo.Setup(r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // NOTE: Act
        var result = await _authService.RefreshTokenAsync(request, "127.0.0.1");

        // NOTE: Assert
        Assert.NotNull(result);
        Assert.Equal("access_token", result.AccessToken);
        Assert.Equal("refresh_token", result.RefreshToken);
        Assert.Equal(user.UserId, result.User.UserId);

        // IMPORTANT: Old token must be revoked (rotation)
        _mockRepo.Verify(r => r.RevokeRefreshTokenAsync(storedToken.RefreshTokenId, It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: New refresh token saved
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: LastLoginAt updated
        _mockRepo.Verify(r => r.UpdateLastLoginAsync(user.UserId, It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: Reuse detection should NOT be triggered
        _mockRepo.Verify(r => r.RevokeAllUserRefreshTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 22 - Non-existent refresh token -> UnauthorizedException
    [Fact]
    public async Task RefreshTokenAsync_WithNonExistentToken_ThrowsUnauthorizedException()
    {
        // NOTE: Arrange
        var request = new RefreshTokenRequestDto
        {
            RefreshToken = "unknown_token"
        };

        // IMPORTANT: Token hash not found in DB
        _mockRepo.Setup(r => r.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.RefreshTokenAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthInvalidRefreshToken, exception.MessageCode);

        // IMPORTANT: No revocation, no new tokens
        _mockRepo.Verify(r => r.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.RevokeAllUserRefreshTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 23 - Reused (already revoked) refresh token -> revoke ALL sessions
    [Fact]
    public async Task RefreshTokenAsync_WithRevokedToken_RevokesAllUserSessionsAndThrows()
    {
        // NOTE: Arrange
        var request = new RefreshTokenRequestDto
        {
            RefreshToken = "stolen_revoked_token"
        };

        var user = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "victim@parishbell.lk",
            IsActive = true,
            AuthProvider = (short)AuthProvider.Email
        };

        // IMPORTANT: Token exists but was already revoked - sign of replay/theft
        var revokedToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            TokenHash = "hashed_refresh",
            ExpiresAt = DateTime.UtcNow.AddDays(15),
            IsRevoked = true, // IMPORTANT: Already revoked!
            RevokedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockRepo.Setup(r => r.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(revokedToken);

        // NOTE: Mock the all-sessions revocation
        _mockRepo.Setup(r => r.RevokeAllUserRefreshTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.RefreshTokenAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthRefreshTokenReuse, exception.MessageCode);

        // IMPORTANT: ALL user sessions must be revoked - this is the compromise mitigation
        _mockRepo.Verify(r => r.RevokeAllUserRefreshTokensAsync(user.UserId, It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: No new tokens issued
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 24 - Expired refresh token -> UnauthorizedException
    [Fact]
    public async Task RefreshTokenAsync_WithExpiredToken_ThrowsUnauthorizedException()
    {
        // NOTE: Arrange
        var request = new RefreshTokenRequestDto
        {
            RefreshToken = "expired_token"
        };

        var user = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "test@parishbell.lk",
            IsActive = true,
            AuthProvider = (short)AuthProvider.Email
        };

        // IMPORTANT: Token is not revoked but expired
        var expiredToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            TokenHash = "hashed_refresh",
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // IMPORTANT: Expired yesterday
            IsRevoked = false
        };

        _mockRepo.Setup(r => r.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(expiredToken);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.RefreshTokenAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthRefreshTokenExpired, exception.MessageCode);

        // IMPORTANT: Expired tokens do NOT trigger all-session revocation - this isn't a reuse attack
        _mockRepo.Verify(r => r.RevokeAllUserRefreshTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        // IMPORTANT: No tokens issued
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 25 - Refresh token belongs to inactive user -> UnauthorizedException
    [Fact]
    public async Task RefreshTokenAsync_WithInactiveUser_ThrowsUnauthorizedException()
    {
        // NOTE: Arrange
        var request = new RefreshTokenRequestDto
        {
            RefreshToken = "valid_token"
        };

        // IMPORTANT: User is deactivated
        var inactiveUser = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "inactive@parishbell.lk",
            IsActive = false,
            AuthProvider = (short)AuthProvider.Email
        };

        var storedToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = inactiveUser.UserId,
            User = inactiveUser,
            TokenHash = "hashed_refresh",
            ExpiresAt = DateTime.UtcNow.AddDays(15),
            IsRevoked = false
        };

        _mockRepo.Setup(r => r.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.RefreshTokenAsync(request, "127.0.0.1"));
        Assert.Equal(MessageCodes.AuthAccountInactive, exception.MessageCode);

        // IMPORTANT: No tokens issued for inactive accounts
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 26 - Verify all rotation side effects on success
    [Fact]
    public async Task RefreshTokenAsync_OnSuccess_PerformsAllRotationSideEffects()
    {
        // NOTE: Arrange
        var request = new RefreshTokenRequestDto
        {
            RefreshToken = "raw_refresh_token"
        };

        var user = new AppUser
        {
            UserId = Guid.NewGuid(),
            Email = "test@parishbell.lk",
            FullName = "Test User",
            IsActive = true,
            PreferredLanguage = _testLanguageId,
            AuthProvider = (short)AuthProvider.Email
        };

        var storedToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            TokenHash = "hashed_refresh",
            ExpiresAt = DateTime.UtcNow.AddDays(15),
            IsRevoked = false
        };

        _mockRepo.Setup(r => r.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);
        _mockRepo.Setup(r => r.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.UpdateLastLoginAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // NOTE: Act
        var result = await _authService.RefreshTokenAsync(request, "192.168.1.100");

        // NOTE: Assert - verify ALL three side effects:

        // NOTE: 1. Old token revoked
        _mockRepo.Verify(r => r.RevokeRefreshTokenAsync(storedToken.RefreshTokenId, It.IsAny<CancellationToken>()), Times.Once);

        // NOTE: 2. New refresh token saved with the IP address
        _mockRepo.Verify(r => r.SaveRefreshTokenAsync(It.Is<RefreshToken>(rt =>
            rt.UserId == user.UserId
            && rt.CreatedByIp == "192.168.1.100"
            && rt.IsRevoked == false
            && rt.TokenHash == "hashed_refresh"
        ), It.IsAny<CancellationToken>()), Times.Once);

        // NOTE: 3. LastLoginAt updated
        _mockRepo.Verify(r => r.UpdateLastLoginAsync(user.UserId, It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: Result contains user info from the existing user
        Assert.Equal(user.UserId, result.User.UserId);
        Assert.Equal("test@parishbell.lk", result.User.Email);
    }

    // NOTE: LOGOUT TESTS

    // IMPORTANT: TEST 27 - Logout revokes the matching refresh token
    [Fact]
    public async Task LogoutAsync_WithValidToken_RevokesTheRefreshToken()
    {
        // NOTE: Arrange
        var request = new LogoutRequestDto
        {
            RefreshToken = "raw_refresh_token"
        };

        // NOTE: An active, unrevoked refresh token in DB
        var storedToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "hashed_refresh", // IMPORTANT: Matches what _mockJwt.HashToken returns
            ExpiresAt = DateTime.UtcNow.AddDays(15),
            IsRevoked = false
        };

        _mockRepo.Setup(r => r.GetRefreshTokenByHashAsync("hashed_refresh", It.IsAny<CancellationToken>())).ReturnsAsync(storedToken);
        _mockRepo.Setup(r => r.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // NOTE: Act
        await _authService.LogoutAsync(request);

        // IMPORTANT: This specific refresh token was revoked
        _mockRepo.Verify(r => r.RevokeRefreshTokenAsync(storedToken.RefreshTokenId, It.IsAny<CancellationToken>()), Times.Once);

        // IMPORTANT: Other user's sessions are NOT touched - only this device signs out
        _mockRepo.Verify(r => r.RevokeAllUserRefreshTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 28 - Logout with non existenting token is idempotent (no error)
    [Fact]
    public async Task LogoutAsync_WithUnknownToken_DoesNotThrow()
    {
        // NOTE: Arrange
        var request = new LogoutRequestDto
        {
            RefreshToken = "unknown_token"
        };

        // IMPORTANT: Token doesn't exist in DB
        _mockRepo.Setup(r => r.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        // NOTE: Act - should NOT throw
        await _authService.LogoutAsync(request);

        // IMPORTANT: No revocation attempts - silent return
        _mockRepo.Verify(r => r.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.RevokeAllUserRefreshTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 29 - Logout with already revoked token is idempotent (no error)
    [Fact]
    public async Task LogoutAsync_WithAlreadyRevokedToken_DoesNotThrow()
    {
        // NOTE: Arrange
        var request = new LogoutRequestDto
        {
            RefreshToken = "already_revoked_token"
        };

        // IMPORTANT: Token exists but was already revoked
        var revokedToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "hashed_refresh",
            ExpiresAt = DateTime.UtcNow.AddDays(15),
            IsRevoked = true, // IMPORTANT: Already revoked
            RevokedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        _mockRepo.Setup(r => r.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(revokedToken);

        // NOTE: Act - should NOT throw
        await _authService.LogoutAsync(request);

        // IMPORTANT: We don't re-revoke an already-revoked token (no double work)
        _mockRepo.Verify(r => r.RevokeRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        // IMPORTANT: We also don't trigger all-session revocation - logout is NOT a reuse attack signal
        _mockRepo.Verify(r => r.RevokeAllUserRefreshTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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