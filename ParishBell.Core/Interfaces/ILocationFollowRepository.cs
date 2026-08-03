namespace ParishBell.Core.Interfaces;

public interface ILocationFollowRepository
{
    // NOTE: True only when the location exists and is live - follows need a live church.
    Task<bool> LocationIsFollowableAsync(Guid locationId, CancellationToken ct = default);

    // NOTE: True when the user currently follows the location.
    Task<bool> IsFollowingAsync(Guid userId, Guid locationId, CancellationToken ct = default);

    // NOTE: Inserts the follow row. Idempotent — a no-op when the user already follows the location.
    Task AddFollowAsync(Guid userId, Guid locationId, CancellationToken ct = default);

    // NOTE: Removes the follow row. Returns false when there was nothing to remove.
    Task<bool> RemoveFollowAsync(Guid userId, Guid locationId, CancellationToken ct = default);
}
