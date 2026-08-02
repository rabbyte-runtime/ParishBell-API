using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface ILiturgicalCalendarRepository
{
    Task<List<LiturgicalCalendarResult>> GetByMonthYearAsync(
        int month,
        int year,
        string languageCode,
        CancellationToken ct = default);
}
