using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class LanguageService(ILanguageRepository languageRepository) : ILanguageService
{
    private readonly ILanguageRepository _languageRepository = languageRepository;

    public async Task<List<LanguageDto>> GetActiveLanguagesAsync(string languageCode, CancellationToken ct)
    {
        var languages = await _languageRepository.GetActiveLanguagesAsync(ct);

        if (languages == null || languages.Count == 0)
        {
            throw new NotFoundException("PB-38");
        }

        var languageDtos = languages.Select(l => new LanguageDto
        {
            LanguageId = l.LanguageId,
            Code = l.LanguageCode,
            Name = l.LanguageName,
            NativeName = l.NativeName
        }).ToList();

        return languageDtos;
    }
}