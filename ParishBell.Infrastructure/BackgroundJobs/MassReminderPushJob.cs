using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParishBell.Core.Configuration;
using ParishBell.Core.Interfaces;

namespace ParishBell.Infrastructure.BackgroundJobs;

public class MassReminderPushJob(
    IServiceScopeFactory scopeFactory,
    MassReminderPushSettings settings,
    ILogger<MassReminderPushJob> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly MassReminderPushSettings _settings = settings;
    private readonly ILogger<MassReminderPushJob> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Mass reminder push job is disabled via configuration.");
            return;
        }

        // NOTE: Floor the interval so a misconfigured value can't hot-loop the job.
        // IMPORTANT: Polling slower than the lookback window would drop reminders entirely - a fire time that passes
        // IMPORTANT:  between two polls is only recovered because the window reaches back further than the gap.
        var interval = TimeSpan.FromSeconds(Math.Max(15, _settings.PollIntervalSeconds));

        // NOTE: Let the app finish starting before the first poll.
        await Task.Delay(interval, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMassReminderNotificationService>();

                var result = await service.ProcessDueAsync(ct);

                if (result.Enqueued > 0 || result.Delivered > 0 || result.Failed > 0)
                    _logger.LogInformation(
                        "Mass reminder push: enqueued {Enqueued}, delivered {Delivered}, failed {Failed}.",
                        result.Enqueued, result.Delivered, result.Failed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Mass reminder push job iteration failed. Retrying in {Seconds}s.", interval.TotalSeconds);
            }

            await Task.Delay(interval, ct);
        }
    }
}
