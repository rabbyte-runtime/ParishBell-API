using ParishBell.Core.Entities;

namespace ParishBell.Core.Interfaces;

public interface ILanguageRepository
{
    // NOTE: Returns all active languages
    Task<List<Language>> GetActiveLanguagesAsync(CancellationToken ct = default);

    // NOTE: True when the id belongs to a language users are allowed to pick (exists and active).
    Task<bool> IsActiveLanguageAsync(Guid languageId, CancellationToken ct = default);
}