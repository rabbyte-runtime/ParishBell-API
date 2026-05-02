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

    // NOTE: General
    public const string GeneralUnexpectedError = "PB-1";
    public const string GeneralNotFound = "PB-2";
    public const string GeneralUnauthorized = "PB-3";
    public const string GeneralForbidden = "PB-4";
}