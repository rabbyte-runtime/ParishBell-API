using ParishBell.Core.DTOs.User;

namespace ParishBell.Core.Interfaces;

public interface IUserNotificationService
{
    // NOTE: One page of the signed-in user's inbox, newest first.
    Task<NotificationPageDto> GetNotificationsAsync(Guid userId, int? page, int? pageSize, CancellationToken ct = default);

    // NOTE: Marks one of the user's notifications read. Throws NotFound when it isn't theirs.
    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    // NOTE: Marks the user's whole inbox read. Idempotent - an already-read inbox succeeds.
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
