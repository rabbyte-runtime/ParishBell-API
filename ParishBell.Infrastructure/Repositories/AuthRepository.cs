using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class AuthRepository(ParishBellDbContext db) : IAuthRepository
{
    private readonly ParishBellDbContext _dbContext = db;

    // NOTE: Check if the email is already in the AppUsers table in DB and return true for existing email, and false if the email doesn't exist
    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await _dbContext.AppUsers.AnyAsync(u => u.Email.Equals(email, StringComparison.InvariantCultureIgnoreCase), ct);

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
}