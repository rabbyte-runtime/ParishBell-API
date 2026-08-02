using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/mass")]
[Authorize]
public class MassController(IMassScheduleService massScheduleService, IMessageCache messages) : ControllerBase
{
    private readonly IMassScheduleService _massScheduleService = massScheduleService;
    private readonly IMessageCache _messages = messages;

    // NOTE: GET /api/v1/mass/schedule
    // IMPORTANT: Requires User JWT. One month of mass times across the churches the caller follows - following nothing returns an empty list, not a 404.
    // NOTE: Optional ?month=(1-12) and ?year= filter to a single month for the calendar UI; both default to the current UTC month/year.
    // NOTE: Mirrors GET /locations/followed/events on purpose - the client merges both onto one month grid, so weekly masses come back already expanded onto dates.
    // NOTE: Each occurrence carries the caller's own reminder (reminderId, minutesBefore, isActive) or null - no second call is needed to render the bells.
    [HttpGet("schedule")]
    public async Task<IActionResult> GetFollowedMassSchedules(
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken ct)
    {
        var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var resolvedMonth = month ?? today.Month;
        var resolvedYear = year ?? today.Year;

        if (resolvedMonth is < 1 or > 12 || resolvedYear is < 1 or > 9999)
            throw new BadRequestException(MessageCodes.MassScheduleInvalidFilter);

        var userId = User.GetUserId();
        var result = await _massScheduleService.GetFollowedMassSchedulesAsync(userId, languageCode, resolvedMonth, resolvedYear, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.MassScheduleRetrieved, result);
        return StatusCode(response.Status, response);
    }
}
