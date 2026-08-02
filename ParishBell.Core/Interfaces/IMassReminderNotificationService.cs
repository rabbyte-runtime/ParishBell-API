using ParishBell.Core.DTOs.Notifications;

namespace ParishBell.Core.Interfaces;

public interface IMassReminderNotificationService
{
    // NOTE: One pass of the reminder outbox: queue whatever has come due since the last look, then deliver what is queued.
    Task<MassReminderPushResult> ProcessDueAsync(CancellationToken ct = default);
}
