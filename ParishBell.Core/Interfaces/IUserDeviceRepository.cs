namespace ParishBell.Core.Interfaces;

public interface IUserDeviceRepository
{
    // NOTE: Idempotent upsert keyed by the globally-unique device token. Re-points the token to the
    //       given user when it already exists (reinstall / account switch on the same device).
    Task UpsertAsync(Guid userId, string token, short platform, string? appVersion, CancellationToken ct = default);

    // NOTE: Removes the token only when it belongs to the given user. Idempotent — a no-op otherwise.
    Task RemoveByTokenAsync(Guid userId, string token, CancellationToken ct = default);

    // NOTE: All device tokens for a single user on the given platform (1=iOS, 2=Android).
    Task<IReadOnlyList<string>> GetTokensAsync(Guid userId, short platform, CancellationToken ct = default);

    // NOTE: All device tokens across the given users on the given platform — used for fan-out pushes.
    Task<IReadOnlyList<string>> GetTokensAsync(IReadOnlyCollection<Guid> userIds, short platform, CancellationToken ct = default);

    // NOTE: Bulk-deletes the given tokens regardless of owner. Used to prune tokens FCM reports as stale.
    Task RemoveByTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken ct = default);
}
