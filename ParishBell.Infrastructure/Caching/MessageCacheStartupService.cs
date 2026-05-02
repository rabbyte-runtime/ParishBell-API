using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParishBell.Core.Interfaces;

namespace ParishBell.Infrastructure.Caching;

public class MessageCacheStartupService(IServiceScopeFactory scopeFactory, ILogger<MessageCacheStartupService> logger) : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<MessageCacheStartupService> _logger = logger;

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting eager message cache load...");

        // NOTE: IMessageCache is scoped - resolve via scope inside singleton hosted service
        using var scope = _scopeFactory.CreateScope();
        var messageCache = scope.ServiceProvider.GetRequiredService<IMessageCache>();
        await messageCache.ReloadAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}