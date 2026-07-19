using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParishBell.Core.Configuration;
using ParishBell.Core.Interfaces;

namespace ParishBell.Infrastructure.BackgroundJobs;

public class AnnouncementPushJob(
    IServiceScopeFactory scopeFactory,
    AnnouncementPushSettings settings,
    ILogger<AnnouncementPushJob> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly AnnouncementPushSettings _settings = settings;
    private readonly ILogger<AnnouncementPushJob> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Announcement push job is disabled via configuration.");
            return;
        }

        // NOTE: Floor the interval so a misconfigured value can't hot-loop the job.
        var interval = TimeSpan.FromSeconds(Math.Max(15, _settings.PollIntervalSeconds));

        // NOTE: Let the app finish starting before the first poll.
        await Task.Delay(interval, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAnnouncementNotificationService>();

                var result = await service.ProcessPendingAsync(ct);

                if (result.Enqueued > 0 || result.Delivered > 0 || result.Failed > 0)
                    _logger.LogInformation(
                        "Announcement push: enqueued {Enqueued}, delivered {Delivered}, failed {Failed}.",
                        result.Enqueued, result.Delivered, result.Failed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Announcement push job iteration failed. Retrying in {Seconds}s.", interval.TotalSeconds);
            }

            await Task.Delay(interval, ct);
        }
    }
}
