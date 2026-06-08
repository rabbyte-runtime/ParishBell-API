// ============================================================
// FILE: ParishBell.Infrastructure/Repositories/PasswordResetRepository.cs
// ============================================================
using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class PasswordResetRepository(ParishBellDbContext db) : IPasswordResetRepository
{
    private readonly ParishBellDbContext _dbContext = db;

    // NOTE: Count tokens created within the time window (used for rate limiting)
    public async Task<int> CountRecentTokensAsync(Guid userId, TimeSpan window, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - window;
        return await _dbContext.PasswordResetTokens.CountAsync(t => t.UserId == userId && t.CreatedAt >= cutoff, ct);
    }

    // NOTE: Save a new reset token
    public async Task SaveResetTokenAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        _dbContext.PasswordResetTokens.Add(token);
        await _dbContext.SaveChangesAsync(ct);
    }

    // NOTE: Get an active (unused + unexpired) token matching the user + code hash
    public async Task<PasswordResetToken?> GetActiveTokenAsync(Guid userId, string codeHash, CancellationToken ct = default) => await _dbContext.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.CodeHash == codeHash && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow, ct);

    // NOTE: Mark a token as used after successful password reset
    public async Task MarkTokenUsedAsync(Guid resetTokenId, CancellationToken ct = default)
    {
        var token = await _dbContext.PasswordResetTokens.FindAsync([resetTokenId], ct);
        if (token is null) return;

        token.IsUsed = true;
        token.UsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    // NOTE: Invalidate ALL active tokens for a user - prevents code reuse
    public async Task InvalidateAllActiveTokensAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _dbContext.PasswordResetTokens.Where(t => t.UserId == userId && !t.IsUsed).ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.IsUsed = true;
            token.UsedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
