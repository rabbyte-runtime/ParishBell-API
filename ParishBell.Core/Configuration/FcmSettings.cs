namespace ParishBell.Core.Configuration;

public class FcmSettings
{
    // NOTE: Path to the Firebase service-account JSON from the Firebase Console.
    // NOTE: Used when CredentialsJson is not set. Pointed at from user-secrets.
    public string CredentialsPath { get; set; } = string.Empty;

    // NOTE: Alternative to CredentialsPath — the service-account JSON supplied inline (e.g. from an
    // NOTE: Takes precedence over CredentialsPath when set.
    public string? CredentialsJson { get; set; }

    // IMPORTANT: When true, pushes are logged instead of sent - dev without credentials.
    public bool MockMode { get; set; } = false;
}
