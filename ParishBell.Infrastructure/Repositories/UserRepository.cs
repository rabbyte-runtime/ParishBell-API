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
                u.PreferredLanguageNavigation.NativeName))
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
}
