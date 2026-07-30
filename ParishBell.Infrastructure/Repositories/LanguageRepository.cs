using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class LanguageRepository(ParishBellDbContext dbContext) : ILanguageRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;

    public async Task<List<Language>> GetActiveLanguagesAsync(CancellationToken ct = default)
    {
        return await _dbContext.Languages.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.LanguageName).ToListAsync(ct);
    }

    public async Task<bool> IsActiveLanguageAsync(Guid languageId, CancellationToken ct = default)
    {
        return await _dbContext.Languages.AsNoTracking().AnyAsync(l => l.LanguageId == languageId && l.IsActive, ct);
    }
}