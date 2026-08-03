using ParishBell.Core.DTOs.Notifications;

namespace ParishBell.Core.Interfaces;

public interface IFeastDayNotificationService
{
    // NOTE: One pass of the feast day outbox: queue whatever falls due today, then deliver what is queued.
    Task<FeastDayPushResult> ProcessDueAsync(CancellationToken ct = default);
}
