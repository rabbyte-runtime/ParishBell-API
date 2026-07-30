using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IUserRepository
{
    // NOTE: The signed-in user's profile joined with their preferred language. Null when the user row is gone.
    //       Returns inactive users too — the caller decides how to treat them.
    Task<UserProfileResult?> GetProfileAsync(Guid userId, CancellationToken ct = default);
}
