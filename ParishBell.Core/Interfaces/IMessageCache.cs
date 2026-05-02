namespace ParishBell.Core.Interfaces;

public interface IMessageCache
{
    // NOTE: Get a message by its code in the requested language
    // NOTE: Falls back to English if the translation does not exist
    string Get(string messageCode, string languageCode = "en");

    // NOTE: Force reload the cache from the database
    // NOTE: Used by the background refresh job
    Task ReloadAsync(CancellationToken ct = default);
}