namespace ParishBell.Core.Interfaces;

// NOTE: Where a user's own uploaded profile photo lives. Provider photos (Google/Apple) never come through here -
// NOTE:  those are external URLs we merely store and hand back.
public interface IProfilePhotoStorage
{
    // NOTE: Normalises the upload (square, re-encoded) and stores it, returning the blob name to persist.
    // NOTE: Overwrites whatever the user had before - one blob per user, so nothing accumulates.
    // NOTE: Throws BadRequest when the bytes are not a readable image, whatever the client claimed the type was.
    Task<string> UploadAsync(Guid userId, Stream content, CancellationToken ct = default);

    // NOTE: Removes the blob. Idempotent - one that is already gone is not an error.
    Task DeleteAsync(string blobName, CancellationToken ct = default);

    // NOTE: A short-lived read URL for a private blob. Expires, so it is generated per response and never stored.
    Task<string> GetReadUrlAsync(string blobName, CancellationToken ct = default);
}
