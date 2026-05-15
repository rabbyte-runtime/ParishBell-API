namespace ParishBell.Core.Constants;

public static class MessageCodes
{
    // NOTE: Auth
    public const string AuthPasswordsDoNotMatch = "PB-5";
    public const string AuthWeakPassword = "PB-6";
    public const string AuthEmailAlreadyExists = "PB-7";
    public const string AuthInvalidCredentials = "PB-8";
    public const string AuthAccountInactive = "PB-9";
    public const string AuthInvalidRefreshToken = "PB-10";
    public const string AuthRefreshTokenExpired = "PB-11";
    public const string AuthRefreshTokenReuse = "PB-12";
    public const string AuthInvalidSocialToken = "PB-13";
    public const string AuthUnsupportedProvider = "PB-14";
    public const string AuthSocialEmailConflict = "PB-15";
    public const string AuthRegisterSuccess = "PB-27";
    public const string AuthGoogleEmailNotVerified = "PB-29";
    public const string AuthWrongProvider = "PB-30";
    public const string AuthLoginSuccess = "PB-31";
    public const string AuthRefreshSuccess = "PB-32";
    public const string AuthLogoutSuccess = "PB-33";

    // NOTE: General
    public const string GeneralUnexpectedError = "PB-1";
    public const string GeneralNotFound = "PB-2";
    public const string GeneralUnauthorized = "PB-3";
    public const string GeneralForbidden = "PB-4";

    // NOTE: Other
    public const string RateLimitExceeded = "PB-16";

    // NOTE: Validations
    public const string ValidationFullNameRequired = "PB-17";
    public const string ValidationFullNameLength = "PB-18";
    public const string ValidationEmailRequired = "PB-19";
    public const string ValidationEmailInvalid = "PB-20";
    public const string ValidationEmailTooLong = "PB-21";
    public const string ValidationPasswordRequired = "PB-22";
    public const string ValidationConfirmPasswordRequired = "PB-23";
    public const string ValidationPreferredLanguageRequired = "PB-24";
    public const string ValidationProviderRequired = "PB-28";
}