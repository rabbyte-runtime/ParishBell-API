using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class AuthRepository(ParishBellDbContext db) : IAuthRepository
{
    private readonly ParishBellDbContext _dbContext = db;

    // NOTE: Check if the email is already in the AppUsers table in DB and return true for existing email, and false if the email doesn't exist
    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => await _dbContext.AppUsers.AnyAsync(u => u.Email == email.ToLower(), ct);

    // NOTE: Add a new mobile app user to AppUsers
    public async Task<AppUser> CreateUserAsync(AppUser user, CancellationToken ct = default)
    {
        _dbContext.AppUsers.Add(user);
        await _dbContext.SaveChangesAsync(ct);
        return user;
    }

    // NOTE: Save a refresh token to the DB
    public async Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct = default)
    {
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync(ct);
    }

    // NOTE: Get user by external auth provider + provider user id
    // IMPORTANT: Used to detect if a Google/Apple user already exists
    public async Task<AppUser?> GetUserByProviderAsync(AuthProvider provider, string providerUserId, CancellationToken ct = default)
        => await _dbContext.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.AuthProvider == (short)provider && u.AuthProviderId == providerUserId && u.IsActive, ct);

    // NOTE: Update LastLoginAt on successful login
    public async Task UpdateLastLoginAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.AppUsers.FindAsync([userId], ct);
        if (user is null) return;

        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    // NOTE: Get user by email (case-insensitive via ToLower)
    // IMPORTANT: Returns null if user doesn't exist OR is inactive
    public async Task<AppUser?> GetUserByEmailAsync(string email, CancellationToken ct = default) => await _dbContext.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email.ToLower(), ct);
}