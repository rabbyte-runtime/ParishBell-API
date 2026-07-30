using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Common;
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
}
