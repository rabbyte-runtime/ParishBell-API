namespace ParishBell.Core.Configuration;

public class BlobStorageSettings
{
    // NOTE: e.g. https://parishbell.blob.core.windows.net - no key, the account is reached with a managed identity.
    public string AccountUrl { get; set; } = string.Empty;

    // NOTE: Containers are split by purpose; this one holds nothing but user profile photos.
    public string ProfilePhotosContainer { get; set; } = "profile-photos";

    // NOTE: The container is private, so every read URL is a short-lived SAS. Matches the access-token lifetime,
    // NOTE:  which means a cached API response outlives its photo URL - clients must not persist it.
    public int SasMinutes { get; set; } = 15;

    // NOTE: Longest edge of the stored photo. Uploads are cropped square and re-encoded to this before they are stored.
    public int PhotoSize { get; set; } = 512;

    // NOTE: Ceiling on what a client may upload, before re-encoding shrinks it.
    public int MaxUploadBytes { get; set; } = 12 * 1024 * 1024;
}
