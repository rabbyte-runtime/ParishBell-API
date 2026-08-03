namespace ParishBell.Core.Interfaces;

// NOTE: Mints short-lived read URLs for private blobs.
// NOTE: Shared by profile photos we store and announcement media the admin uploaded.
public interface IBlobUrlSigner
{
    // NOTE: A signed read URL for a blob this API knows the location of.
    Task<string> SignAsync(string containerName, string blobName, CancellationToken ct = default);

    // NOTE: Re-signs a URL stored with a SAS baked in at upload, which has expired.
    // IMPORTANT: Returns the input untouched when the blob is not on our own account.
    // NOTE: An external or CDN URL is not ours to sign, and mangling it breaks media.
    Task<string?> ResignAsync(string? storedUrl, CancellationToken ct = default);
}
