using ParishBell.Core.Enums;

namespace ParishBell.Core.Interfaces;

public interface IMessageRepository
{
    // NOTE: Load all messages with all their translations from the DB
    // NOTE: Returns Dictionary[messageCode, Dictionary[languageCode, messageText]]
    Task<Dictionary<string, CachedMessage>> LoadAllAsync(CancellationToken ct = default);

    // IMPORTANT: Internal DTO for the cache layer - holds the message type and per-language translations for a single message code
    public class CachedMessage
    {
        public MessageType Type { get; set; }
        public Dictionary<string, string> Translations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}