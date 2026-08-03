using ParishBell.Core.DTOs.Notifications;

namespace ParishBell.Core.Interfaces;

public interface IMassReminderNotificationService
{
    // NOTE: One pass of the reminder outbox - queue what is due, then deliver it.
    Task<MassReminderPushResult> ProcessDueAsync(CancellationToken ct = default);
}
