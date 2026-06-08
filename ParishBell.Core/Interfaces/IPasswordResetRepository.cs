using ParishBell.Core.Entities;

namespace ParishBell.Core.Interfaces;

public interface IPasswordResetRepository
{
    // NOTE: Count active reset tokens for a user in the last hour (rate limit check)
    Task<int> CountRecentTokensAsync(Guid userId, TimeSpan window, CancellationToken ct = default);

    // NOTE: Save a new reset token
    Task SaveResetTokenAsync(PasswordResetToken token, CancellationToken ct = default);

    // NOTE: Find an active reset token by code hash + user id
    Task<PasswordResetToken?> GetActiveTokenAsync(Guid userId, string codeHash, CancellationToken ct = default);

    // NOTE: Mark a token as used
    Task MarkTokenUsedAsync(Guid resetTokenId, CancellationToken ct = default);

    // NOTE: Invalidate all active tokens for a user (called after successful reset)
    Task InvalidateAllActiveTokensAsync(Guid userId, CancellationToken ct = default);
}
