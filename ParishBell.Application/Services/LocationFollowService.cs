using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Location;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class LocationFollowService(ILocationFollowRepository followRepository, IMassReminderRepository reminderRepository) : ILocationFollowService
{
    private readonly ILocationFollowRepository _followRepository = followRepository;

    // NOTE: Unfollowing must reach reminders too - they have no link to follow state.
    private readonly IMassReminderRepository _reminderRepository = reminderRepository;

    public async Task<FollowStatusDto> GetFollowStatusAsync(Guid userId, Guid locationId, CancellationToken ct = default)
    {
        bool isFollowing = await _followRepository.IsFollowingAsync(userId, locationId, ct);
        return new FollowStatusDto { IsFollowing = isFollowing };
    }

    public async Task FollowLocationAsync(Guid userId, Guid locationId, CancellationToken ct = default)
    {
        // NOTE: A user may only follow a church that is live.
        if (!await _followRepository.LocationIsFollowableAsync(locationId, ct))
            throw new NotFoundException(MessageCodes.LocationNotFound);

        await _followRepository.AddFollowAsync(userId, locationId, ct);
    }

    public async Task UnfollowLocationAsync(Guid userId, Guid locationId, CancellationToken ct = default)
    {
        // NOTE: Idempotent - no location existence check needed; removing a missing follow is a no-op.
        await _followRepository.RemoveFollowAsync(userId, locationId, ct);

        // IMPORTANT: Reminders survive an unfollow and would keep pushing from a church left behind.
        // NOTE: An alert the user can no longer trace to anything on screen.
        // NOTE: Cancelled, not deleted, so the reminders list still shows them for one-tap revival.
        await _reminderRepository.DisableForLocationAsync(userId, locationId, ct);
    }
}
