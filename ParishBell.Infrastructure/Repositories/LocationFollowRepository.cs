using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class LocationFollowRepository(ParishBellDbContext dbContext) : ILocationFollowRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;

    public async Task<bool> LocationIsFollowableAsync(Guid locationId, CancellationToken ct = default)
    {
        return await _dbContext.Locations
            .AsNoTracking()
            .AnyAsync(l => l.LocationId == locationId && l.IsApproved && l.IsActive, ct);
    }

    public async Task AddFollowAsync(Guid userId, Guid locationId, CancellationToken ct = default)
    {
        // NOTE: Skip the insert when the follow already exists — keeps the call idempotent.
        bool alreadyFollowing = await _dbContext.UserFollowedLocations
            .AsNoTracking()
            .AnyAsync(f => f.UserId == userId && f.LocationId == locationId, ct);

        if (alreadyFollowing) return;

        _dbContext.UserFollowedLocations.Add(new UserFollowedLocation
        {
            UserId = userId,
            LocationId = locationId,
            FollowedAt = DateTime.UtcNow
        });

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // NOTE: A concurrent request inserted the same composite PK between the check and the save.
            //       The desired end state already holds, so treat it as success.
        }
    }

    public async Task<bool> RemoveFollowAsync(Guid userId, Guid locationId, CancellationToken ct = default)
    {
        var follow = await _dbContext.UserFollowedLocations
            .FirstOrDefaultAsync(f => f.UserId == userId && f.LocationId == locationId, ct);

        if (follow is null) return false;

        _dbContext.UserFollowedLocations.Remove(follow);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
}
