using ParishBell.Core.DTOs.User;

namespace ParishBell.Core.Interfaces;

public interface IUserService
{
    // NOTE: Profile of the currently signed-in user, resolved from the JWT subject.
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default);

    // NOTE: Applies the supplied fields to the signed-in user and returns the stored profile.
    //       Omitted fields are left alone; a request that changes nothing is a no-op.
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken ct = default);

    // NOTE: The signed-in user's push opt-ins.
    Task<NotificationPreferencesDto> GetNotificationPreferencesAsync(Guid userId, CancellationToken ct = default);

    // NOTE: Applies the supplied switches and returns the stored set. Omitted switches are left alone.
    Task<NotificationPreferencesDto> UpdateNotificationPreferencesAsync(Guid userId, UpdateNotificationPreferencesRequestDto request, CancellationToken ct = default);

    // IMPORTANT: Permanently deletes the signed-in user, but only after they re-prove their credential.
    Task DeleteAccountAsync(Guid userId, DeleteAccountRequestDto request, CancellationToken ct = default);
}
