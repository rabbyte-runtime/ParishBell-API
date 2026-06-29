using ParishBell.Core.Constants;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class LocationFollowService(ILocationFollowRepository followRepository) : ILocationFollowService
{
    private readonly ILocationFollowRepository _followRepository = followRepository;

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
    }
}
