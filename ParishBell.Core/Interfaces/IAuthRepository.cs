using ParishBell.Core.Entities;
using ParishBell.Core.Enums;

namespace ParishBell.Core.Interfaces;

public interface IAuthRepository
{
    // NOTE: Check if the email is already registered
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    // NOTE: Creates a new mobile app user
    Task<AppUser> CreateUserAsync(AppUser user, CancellationToken ct = default);

    // NOTE: Saves a given refresh token
    Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);

    // NOTE: Check if a user already exists with the given social provider + provider user id
    Task<AppUser?> GetUserByProviderAsync(AuthProvider provider, string providerUserId, CancellationToken ct = default);

    // NOTE: New - Get user by email (used for login)
    Task<AppUser?> GetUserByEmailAsync(string email, CancellationToken ct = default);

    // NOTE: New - Update LastLoginAt timestamp on successful login
    Task UpdateLastLoginAsync(Guid userId, CancellationToken ct = default);
}