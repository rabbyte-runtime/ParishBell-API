using ParishBell.Core.DTOs.Push;

namespace ParishBell.Core.Interfaces;

public interface IPushNotificationService
{
    // NOTE: Sends to every registered device of one user. No-op when they have none.
    Task<PushSendResult> SendToUserAsync(Guid userId, PushNotification notification, CancellationToken ct = default);

    // NOTE: Fan-out to every registered device of the given users.
    Task<PushSendResult> SendToUsersAsync(IReadOnlyCollection<Guid> userIds, PushNotification notification, CancellationToken ct = default);
}
