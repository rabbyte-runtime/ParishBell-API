using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;
using static ParishBell.Core.Interfaces.IMessageRepository;

namespace ParishBell.Infrastructure.Repositories;

public class MessageRepository(ParishBellDbContext db) : IMessageRepository
{
    private readonly ParishBellDbContext _db = db;

    public async Task<Dictionary<string, CachedMessage>> LoadAllAsync(CancellationToken ct = default)
    {
        // NOTE: Load messages + their translations + language codes
        var rows = await _db.Messages
            .AsNoTracking()
            .SelectMany(m => m.MessageTranslations, (m, t) => new
            {
                m.MessageCode,
                m.MessageType,
                t.Language!.LanguageCode,
                t.Message,
            })
            .ToListAsync(ct);

        // NOTE: Build the cache structure
        var result = new Dictionary<string, CachedMessage>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!result.TryGetValue(row.MessageCode, out var cached))
            {
                // NOTE: Parse the string MessageType column into the Enum
                cached = new CachedMessage
                {
                    Type = Enum.TryParse<MessageType>(row.MessageType, true, out var t) ? t : MessageType.Error,
                };

                result[row.MessageCode] = cached;
            }

            cached.Translations[row.LanguageCode] = row.Message;
        }

        return result;
    }
}
