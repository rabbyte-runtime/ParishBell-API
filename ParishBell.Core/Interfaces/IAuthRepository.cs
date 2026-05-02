using ParishBell.Core.Entities;

namespace ParishBell.Core.Interfaces;

public interface IAuthRepository
{
    // NOTE: Check if the email is already registered
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    // NOTE: Creates a new mobile app user
    Task<AppUser> CreateUserAsync(AppUser user, CancellationToken ct = default);

    // NOTE: Saves a given refresh token
    Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
}