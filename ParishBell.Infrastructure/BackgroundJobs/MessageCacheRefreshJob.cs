using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParishBell.Core.Interfaces;

namespace ParishBell.Infrastructure.BackgroundJobs;

public class MessageCacheRefreshJob(IServiceScopeFactory scopeFactory, ILogger<MessageCacheRefreshJob> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<MessageCacheRefreshJob> _logger = logger;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // NOTE: Wait for first interval before refreshing
        await Task.Delay(RefreshInterval, ct);

        while (!ct.IsCancellationRequested)
        {
            _logger.LogInformation("Message cache refresh job triggered.");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var messageCache = scope.ServiceProvider.GetRequiredService<IMessageCache>();

                await messageCache.ReloadAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Message cache refresh failed. Will retry in {Hours} hours.", RefreshInterval.TotalHours);
            }

            await Task.Delay(RefreshInterval, ct);
        }
    }
}