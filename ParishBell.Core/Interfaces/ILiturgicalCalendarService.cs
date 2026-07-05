using ParishBell.Core.DTOs.Liturgical;

namespace ParishBell.Core.Interfaces;

public interface ILiturgicalCalendarService
{
    Task<LiturgicalCalendarListDto> GetByMonthYearAsync(
        int month,
        int year,
        string languageCode,
        CancellationToken ct = default);
}
