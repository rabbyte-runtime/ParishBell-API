using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class UserRepository(ParishBellDbContext dbContext) : IUserRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;

    public async Task<UserProfileResult?> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        // NOTE: Projected in one query - the language join comes along instead of a second round trip.
        return await _dbContext.AppUsers
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new UserProfileResult(
                u.UserId,
                u.FullName,
                u.Email,
                u.ProfileImageUrl,
                u.AuthProvider,
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt,
                u.PreferredLanguage,
                u.PreferredLanguageNavigation.LanguageCode,
                u.PreferredLanguageNavigation.LanguageName,
                u.PreferredLanguageNavigation.NativeName,
                u.NotifyEvents,
                u.NotifyAnnouncements,
                u.NotifyMassReminders,
                u.NotifyFeastDays))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> EmailTakenByAnotherUserAsync(Guid userId, string email, CancellationToken ct = default)
    {
        return await _dbContext.AppUsers
            .AsNoTracking()
            .AnyAsync(u => u.Email == email.ToLower() && u.UserId != userId, ct);
    }

    public async Task UpdateProfileAsync(Guid userId, string? fullName, string? email, Guid? preferredLanguage, CancellationToken ct = default)
    {
        var user = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null) return;

        // NOTE: Null means the caller left the field out - only supplied values are written.
        if (fullName is not null) user.FullName = fullName;
        if (email is not null) user.Email = email;
        if (preferredLanguage.HasValue) user.PreferredLanguage = preferredLanguage.Value;

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // NOTE: The caller already checked the address was free. A failure here means a concurrent request claimed it first - uq_app_users_email is the only constraint this write can break.
            throw new ConflictException(MessageCodes.AuthEmailAlreadyExists);
        }
    }

    public async Task UpdateNotificationPreferencesAsync(Guid userId, bool? events, bool? announcements, bool? massReminders, bool? feastDays, CancellationToken ct = default)
    {
        var user = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null) return;

        // NOTE: Null means the caller left the switch out - only supplied values are written.
        if (events.HasValue) user.NotifyEvents = events.Value;
        if (announcements.HasValue) user.NotifyAnnouncements = announcements.Value;
        if (massReminders.HasValue) user.NotifyMassReminders = massReminders.Value;
        if (feastDays.HasValue) user.NotifyFeastDays = feastDays.Value;

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken ct = default)
    {
        // IMPORTANT: The children are removed explicitly rather than leaning on the FKs' ON DELETE behaviour
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        // NOTE: Devices go first so the fan-out job can no longer pick this user up mid-delete.
        await _dbContext.UserDevices.Where(d => d.UserId == userId).ExecuteDeleteAsync(ct);
        await _dbContext.UserFollowedLocations.Where(f => f.UserId == userId).ExecuteDeleteAsync(ct);
        await _dbContext.UserMassReminders.Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);
        await _dbContext.NotificationsLogs.Where(n => n.UserId == userId).ExecuteDeleteAsync(ct);
        await _dbContext.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await _dbContext.PasswordResetTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await _dbContext.AppUsers.Where(u => u.UserId == userId).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
