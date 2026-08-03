using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/liturgical-calendar")]
public class LiturgicalCalendarController(ILiturgicalCalendarService calendarService, IMessageCache messages, ILogger<LiturgicalCalendarController> logger) : ApiControllerBase(logger)
{
    private readonly ILiturgicalCalendarService _calendarService = calendarService;
    private readonly IMessageCache _messages = messages;

    // NOTE: GET /api/v1/liturgical-calendar
    // IMPORTANT: Requires User JWT.
    // NOTE: Optional ?month= and ?year= filter the calendar, defaulting to the current month.
    // NOTE: Returns every entry occurring that month, ordered by day.
    // NOTE: Recurring annual feasts plus one-off entries dated inside the month.
    [Authorize]
    [HttpGet]
    public Task<IActionResult> GetCalendar(
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken ct) =>
        ExecuteAsync(nameof(GetCalendar), async () =>
        {
            var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var resolvedMonth = month ?? today.Month;
            var resolvedYear = year ?? today.Year;

            if (resolvedMonth is < 1 or > 12 || resolvedYear is < 1 or > 9999)
                throw new BadRequestException(MessageCodes.LiturgicalCalendarInvalidFilter);

            var result = await _calendarService.GetByMonthYearAsync(resolvedMonth, resolvedYear, languageCode, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LiturgicalCalendarRetrieved, result);
            return StatusCode(response.Status, response);
        }, month, year);
}
