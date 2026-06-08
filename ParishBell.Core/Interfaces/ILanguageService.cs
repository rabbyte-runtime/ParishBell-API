using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface ILanguageService
{
    // NOTE: Returns all active languages
    Task<List<LanguageDto>> GetActiveLanguagesAsync(string languageCode, CancellationToken ct = default);
}