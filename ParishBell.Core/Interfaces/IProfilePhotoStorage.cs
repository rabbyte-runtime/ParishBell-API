namespace ParishBell.Core.Interfaces;

// NOTE: Where an uploaded profile photo lives.
// NOTE: Provider photos never come through here - those are URLs we store and hand back.
public interface IProfilePhotoStorage
{
    // NOTE: Normalises the upload and stores it, returning the blob name to persist.
    // NOTE: Overwrites whatever the user had before - one blob per user, so nothing accumulates.
    // NOTE: Throws BadRequest when the bytes are not a readable image.
    Task<string> UploadAsync(Guid userId, Stream content, CancellationToken ct = default);

    // NOTE: Removes the blob. Idempotent - one that is already gone is not an error.
    Task DeleteAsync(string blobName, CancellationToken ct = default);

    // NOTE: A short-lived read URL - generated per response and never stored.
    Task<string> GetReadUrlAsync(string blobName, CancellationToken ct = default);
}
