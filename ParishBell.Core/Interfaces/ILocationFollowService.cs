namespace ParishBell.Core.Interfaces;

public interface ILocationFollowService
{
    // NOTE: Follows a location for the user. Idempotent — re-following succeeds silently.
    Task FollowLocationAsync(Guid userId, Guid locationId, CancellationToken ct = default);

    // NOTE: Unfollows a location for the user. Idempotent — unfollowing a non-followed location succeeds silently.
    Task UnfollowLocationAsync(Guid userId, Guid locationId, CancellationToken ct = default);
}
