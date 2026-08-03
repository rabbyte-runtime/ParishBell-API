using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class UserDeviceRepository(ParishBellDbContext dbContext) : IUserDeviceRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;

    public async Task UpsertAsync(Guid userId, string token, short platform, string? appVersion, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // NOTE: device_token is globally unique, so an existing token is re-pointed at this user.
        // NOTE: The same device may have been reinstalled or signed into another account since.
        var existing = await _dbContext.UserDevices
            .FirstOrDefaultAsync(d => d.DeviceToken == token, ct);

        if (existing is not null)
        {
            existing.UserId = userId;
            existing.Platform = platform;
            existing.AppVersion = appVersion;
            existing.LastActiveAt = now;
            await _dbContext.SaveChangesAsync(ct);
            return;
        }

        var device = new UserDevice
        {
            UserId = userId,
            DeviceToken = token,
            Platform = platform,
            AppVersion = appVersion,
            RegisteredAt = now,
            LastActiveAt = now
        };

        _dbContext.UserDevices.Add(device);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // NOTE: A concurrent request inserted the same device_token before this save.
            // NOTE: Drop our failed insert, then update the row that won the race.
            _dbContext.Entry(device).State = EntityState.Detached;

            var raced = await _dbContext.UserDevices
                .FirstOrDefaultAsync(d => d.DeviceToken == token, ct);

            if (raced is not null)
            {
                raced.UserId = userId;
                raced.Platform = platform;
                raced.AppVersion = appVersion;
                raced.LastActiveAt = now;
                await _dbContext.SaveChangesAsync(ct);
            }
        }
    }

    public async Task RemoveByTokenAsync(Guid userId, string token, CancellationToken ct = default)
    {
        // NOTE: Scoped to the owner so a caller cannot delete another user's device row. Idempotent.
        var device = await _dbContext.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceToken == token, ct);

        if (device is null) return;

        _dbContext.UserDevices.Remove(device);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetTokensAsync(Guid userId, short platform, CancellationToken ct = default)
    {
        return await _dbContext.UserDevices
            .Where(d => d.UserId == userId && d.Platform == platform)
            .Select(d => d.DeviceToken)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetTokensAsync(IReadOnlyCollection<Guid> userIds, short platform, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return [];

        return await _dbContext.UserDevices
            .Where(d => userIds.Contains(d.UserId) && d.Platform == platform)
            .Select(d => d.DeviceToken)
            .ToListAsync(ct);
    }

    public async Task RemoveByTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken ct = default)
    {
        if (tokens.Count == 0) return;

        // NOTE: FCM reported these unregistered, so the delete is not scoped to a user.
        // NOTE: ExecuteDelete issues a single set-based DELETE.
        await _dbContext.UserDevices
            .Where(d => tokens.Contains(d.DeviceToken))
            .ExecuteDeleteAsync(ct);
    }
}
