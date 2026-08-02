using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Location;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class LocationFollowService(ILocationFollowRepository followRepository, IMassReminderRepository reminderRepository) : ILocationFollowService
{
    private readonly ILocationFollowRepository _followRepository = followRepository;

    // NOTE: Unfollowing has to reach the user's reminders too - the row has no link to follow state of its own.
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

        // IMPORTANT: Mass reminders survive an unfollow on their own, and would keep pushing from a church the user
        // IMPORTANT:  has left - an alert they can no longer trace to anything on screen. Cancel them with the follow.
        // NOTE: Cancelled, not deleted, so GET /users/me/reminders still shows them and one tap puts them back.
        await _reminderRepository.DisableForLocationAsync(userId, locationId, ct);
    }
}
