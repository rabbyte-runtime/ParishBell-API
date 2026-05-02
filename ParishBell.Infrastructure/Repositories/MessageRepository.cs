using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class MessageRepository(ParishBellDbContext db) : IMessageRepository
{
    private readonly ParishBellDbContext _db = db;

    public async Task<Dictionary<string, Dictionary<string, string>>> LoadAllAsync(CancellationToken ct = default)
    {
        // NOTE: Join messages + translations + languages
        var rows = await _db.Messages
            .AsNoTracking()
            .SelectMany(m => m.MessageTranslations, (m, t) => new
            {
                m.MessageCode,
                LanguageCode = t.Language != null ? t.Language.LanguageCode : string.Empty,
                t.Message,
            })
            .ToListAsync(ct);

        // NOTE: Build nested dictionary -- [messageCode][languageCode] = messageText
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!result.TryGetValue(row.MessageCode, out var translations))
            {
                translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result[row.MessageCode] = translations;
            }

            translations[row.LanguageCode] = row.Message;
        }

        return result;
    }
}
