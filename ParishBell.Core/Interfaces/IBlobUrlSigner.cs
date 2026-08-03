namespace ParishBell.Core.Interfaces;

// NOTE: Mints short-lived read URLs for private blobs. Shared by everything that hands a blob to a client - profile
// NOTE:  photos this API stores itself, and announcement media the admin backend uploaded.
public interface IBlobUrlSigner
{
    // NOTE: A signed read URL for a blob this API knows the location of.
    Task<string> SignAsync(string containerName, string blobName, CancellationToken ct = default);

    // NOTE: Re-signs a URL that was stored with a SAS baked in at upload time, which has almost certainly expired.
    // IMPORTANT: Returns the input untouched when it is not a blob on our own account - an external or CDN URL is not
    // IMPORTANT:  ours to sign, and mangling it would break media that currently works.
    Task<string?> ResignAsync(string? storedUrl, CancellationToken ct = default);
}
