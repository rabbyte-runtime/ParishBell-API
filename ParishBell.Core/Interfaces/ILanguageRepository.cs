using ParishBell.Core.Entities;

namespace ParishBell.Core.Interfaces;

public interface ILanguageRepository
{
    // NOTE: Returns all active languages
    Task<List<Language>> GetActiveLanguagesAsync(CancellationToken ct = default);
}