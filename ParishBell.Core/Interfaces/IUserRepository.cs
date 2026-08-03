using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IUserRepository
{
    // NOTE: The profile joined with the preferred language. Null when the row is gone.
    // NOTE: Returns inactive users too - the caller decides how to treat them.
    Task<UserProfileResult?> GetProfileAsync(Guid userId, CancellationToken ct = default);

    // NOTE: True when the email is taken by someone else, not by the caller.
    Task<bool> EmailTakenByAnotherUserAsync(Guid userId, string email, CancellationToken ct = default);

    // NOTE: Writes only non-null arguments; null means leave unchanged.
    Task UpdateProfileAsync(Guid userId, string? fullName, string? email, Guid? preferredLanguage, CancellationToken ct = default);

    // NOTE: Points at an uploaded photo, or clears it so the provider photo returns.
    Task UpdateProfilePhotoBlobAsync(Guid userId, string? blobName, CancellationToken ct = default);

    // NOTE: Stores the latest provider photo, refreshed at every social login.
    Task UpdateProviderPhotoUrlAsync(Guid userId, string? imageUrl, CancellationToken ct = default);

    // NOTE: Writes only supplied switches; null means leave unchanged.
    Task UpdateNotificationPreferencesAsync(Guid userId, bool? events, bool? announcements, bool? massReminders, bool? feastDays, CancellationToken ct = default);

    // IMPORTANT: Removes the user and every child row in one transaction. Not recoverable.
    Task DeleteAccountAsync(Guid userId, CancellationToken ct = default);
}
