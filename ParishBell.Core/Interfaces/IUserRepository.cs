using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IUserRepository
{
    // NOTE: The signed-in user's profile joined with their preferred language. Null when the user row is gone.
    //       Returns inactive users too — the caller decides how to treat them.
    Task<UserProfileResult?> GetProfileAsync(Guid userId, CancellationToken ct = default);

    // NOTE: True when the email is already taken by someone else — the caller's own address is not a conflict.
    Task<bool> EmailTakenByAnotherUserAsync(Guid userId, string email, CancellationToken ct = default);

    // NOTE: Writes only the non-null arguments; nulls mean "leave unchanged". No-op when the user row is gone.
    Task UpdateProfileAsync(Guid userId, string? fullName, string? email, Guid? preferredLanguage, CancellationToken ct = default);

    // IMPORTANT: Permanently removes the user and everything hanging off them, in one transaction. Not recoverable.
    Task DeleteAccountAsync(Guid userId, CancellationToken ct = default);
}
