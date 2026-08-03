using ParishBell.Core.Enums;

namespace ParishBell.Core.Interfaces;

public interface IMessageRepository
{
    // NOTE: Load all messages with all their translations from the DB
    // NOTE: Returns Dictionary[messageCode, Dictionary[languageCode, messageText]]
    Task<Dictionary<string, CachedMessage>> LoadAllAsync(CancellationToken ct = default);

    // IMPORTANT: Internal cache DTO - the message type plus its per-language translations.
    public class CachedMessage
    {
        public MessageType Type { get; set; }
        public Dictionary<string, string> Translations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}