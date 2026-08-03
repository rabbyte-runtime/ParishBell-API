using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParishBell.Core.Configuration;
using ParishBell.Core.Interfaces;

namespace ParishBell.Infrastructure.BackgroundJobs;

public class FeastDayPushJob(
    IServiceScopeFactory scopeFactory,
    FeastDayPushSettings settings,
    ILogger<FeastDayPushJob> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly FeastDayPushSettings _settings = settings;
    private readonly ILogger<FeastDayPushJob> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Feast day push job is disabled via configuration.");
            return;
        }

        // NOTE: Floor the interval so a misconfigured value can't hot-loop the job.
        // NOTE: Polls far less often than the reminder job - the send window is hours, not minutes.
        var interval = TimeSpan.FromSeconds(Math.Max(60, _settings.PollIntervalSeconds));

        // NOTE: Let the app finish starting before the first poll.
        await Task.Delay(interval, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IFeastDayNotificationService>();

                var result = await service.ProcessDueAsync(ct);

                if (result.Enqueued > 0 || result.Delivered > 0 || result.Failed > 0)
                    _logger.LogInformation(
                        "Feast day push: enqueued {Enqueued}, delivered {Delivered}, failed {Failed}.",
                        result.Enqueued, result.Delivered, result.Failed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Feast day push job iteration failed. Retrying in {Seconds}s.", interval.TotalSeconds);
            }

            await Task.Delay(interval, ct);
        }
    }
}
