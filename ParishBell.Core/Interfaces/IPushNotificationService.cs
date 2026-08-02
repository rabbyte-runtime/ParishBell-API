using ParishBell.Core.DTOs.Push;

namespace ParishBell.Core.Interfaces;

public interface IPushNotificationService
{
    // NOTE: Sends the notification to every registered device of a single user. No-op (Empty result) when the user has no tokens.
    Task<PushSendResult> SendToUserAsync(Guid userId, PushNotification notification, CancellationToken ct = default);

    // NOTE: Fan-out send to every registered device of the given users (e.g. announcement to a location's followers).
    Task<PushSendResult> SendToUsersAsync(IReadOnlyCollection<Guid> userIds, PushNotification notification, CancellationToken ct = default);
}
