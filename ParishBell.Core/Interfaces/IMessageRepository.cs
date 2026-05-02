namespace ParishBell.Core.Interfaces;

public interface IMessageRepository
{
    // NOTE: Load all messages with all their translations from the DB
    // NOTE: Returns Dictionary[messageCode, Dictionary[languageCode, messageText]]
    Task<Dictionary<string, Dictionary<string, string>>> LoadAllAsync(CancellationToken ct = default);
}