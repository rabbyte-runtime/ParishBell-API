using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;
using static ParishBell.Core.Interfaces.IMessageRepository;

namespace ParishBell.Infrastructure.Caching;

public class MessageCache(IMessageRepository repo, IMemoryCache cache, ILogger<MessageCache> logger) : IMessageCache
{
    private readonly IMessageRepository _repo = repo;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<MessageCache> _logger = logger;

    private const string CacheKey = "parishbell_messages";
    private const string FallbackLanguage = "en";

    public string GetText(string messageCode, string languageCode = FallbackLanguage)
    {
        if (!_cache.TryGetValue(CacheKey, out Dictionary<string, Dictionary<string, string>>? messages) || messages is null)
        {
            // IMPORTANT: Cache miss - should not happen after startup but handle gracefully
            _logger.LogWarning("Message cache miss for code '{Code}'. Cache may not be loaded yet.", messageCode);
            return messageCode; // NOTE: Return code
        }

        if (!messages.TryGetValue(messageCode, out var translations))
        {
            _logger.LogWarning("Message code '{Code}' not found in cache.", messageCode);
            return messageCode;
        }

        // NOTE: Try requested language first - else, fall back to English
        if (translations.TryGetValue(languageCode, out var text))
            return text;

        if (translations.TryGetValue(FallbackLanguage, out var fallback))
        {
            _logger.LogDebug("No '{Lang}' translation for '{Code}'. Using English fallback.", languageCode, messageCode);
            return fallback;
        }

        // NOTE: No translation - return the code itself
        _logger.LogWarning("No translation found for '{Code}' in any language.", messageCode);
        return messageCode;
    }

    public MessageType GetType(string messageCode)
    {
        if (!TryGetCachedMessages(out var messages) || !messages!.TryGetValue(messageCode, out var cached))
        {
            // IMPORTANT: Default to Error if the code is unknown
            return MessageType.Error;
        }

        return cached.Type;
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Reloading message cache from database...");

        try
        {
            var messages = await _repo.LoadAllAsync(ct);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(12))
                .SetPriority(CacheItemPriority.NeverRemove);

            _cache.Set(CacheKey, messages, cacheOptions);
            _logger.LogInformation("Message cache loaded - {Count} message codes cached.", messages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload message cache from database.");
            throw;
        }
    }

    // NOTE: Private helper method to try and get the messages from memory cache
    private bool TryGetCachedMessages(out Dictionary<string, CachedMessage>? messages)
    {
        if (_cache.TryGetValue(CacheKey, out messages) && messages is not null)
            return true;

        _logger.LogWarning("Message cache miss. Cache may not be loaded yet.");
        messages = null;
        return false;
    }
}