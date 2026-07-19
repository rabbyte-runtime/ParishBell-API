namespace ParishBell.Core.Configuration;

public class FcmSettings
{
    // NOTE: Path to the Firebase service-account JSON (Firebase Console > Project settings > Service accounts).
    //       Used when CredentialsJson is not set. Stored/pointed at from user-secrets.
    public string CredentialsPath { get; set; } = string.Empty;

    // NOTE: Alternative to CredentialsPath — the service-account JSON supplied inline (e.g. from an
    //       environment variable or Key Vault). Takes precedence over CredentialsPath when set.
    public string? CredentialsJson { get; set; }

    // IMPORTANT: When true, pushes are logged to the console instead of sent (dev/testing without Firebase credentials).
    public bool MockMode { get; set; } = false;
}
