using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Mass;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/mass")]
[Authorize]
public class MassController(
    IMassScheduleService massScheduleService,
    IMassReminderService reminderService,
    IMessageCache messages,
    ILogger<MassController> logger) : ApiControllerBase(logger)
{
    private readonly IMassScheduleService _massScheduleService = massScheduleService;
    private readonly IMassReminderService _reminderService = reminderService;
    private readonly IMessageCache _messages = messages;

    // NOTE: GET /api/v1/mass/schedule
    // IMPORTANT: Requires User JWT. One month of mass times across the churches the caller follows - following nothing returns an empty list, not a 404.
    // NOTE: Optional ?month=(1-12) and ?year= filter to a single month for the calendar UI; both default to the current UTC month/year.
    // NOTE: Mirrors GET /locations/followed/events on purpose - the client merges both onto one month grid, so weekly masses come back already expanded onto dates.
    // NOTE: Each occurrence carries the caller's own reminder (reminderId, minutesBefore, isActive) or null - no second call is needed to render the bells.
    [HttpGet("schedule")]
    public Task<IActionResult> GetFollowedMassSchedules(
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken ct) =>
        ExecuteAsync(nameof(GetFollowedMassSchedules), async () =>
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
        }, month, year);

    // NOTE: POST /api/v1/mass/reminders
    // IMPORTANT: Requires User JWT. Always sets the reminder for the token's own user - the caller cannot set one for another.
    // NOTE: Idempotent upsert keyed by (user, schedule) - posting again re-times an existing reminder and switches a disabled one back on, rather than failing on uq_user_schedule.
    // NOTE: Returns the saved reminder so the calendar can rebind the bell without re-fetching the month.
    // NOTE: 404 when the mass does not exist or its church is no longer visible, 422 when minutesBefore falls outside 1-1440.
    [HttpPost("reminders")]
    public Task<IActionResult> SetMassReminder([FromBody] SetMassReminderRequestDto request, CancellationToken ct) =>
        ExecuteAsync(nameof(SetMassReminder), async () =>
        {
            var userId = User.GetUserId();
            var result = await _reminderService.SetReminderAsync(userId, request, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.MassReminderSaved, result);
            return StatusCode(response.Status, response);
        }, request.ScheduleId, request.MinutesBefore);

    // NOTE: DELETE /api/v1/mass/reminders/{reminderId}
    // IMPORTANT: Requires User JWT. Scoped to the caller - another user's reminder id returns 404, not 403.
    // IMPORTANT: Cancels rather than erases: the row is kept switched off, so the schedule endpoint reports it as isActive:false and POSTing again revives it.
    // NOTE: Idempotent - cancelling an already-cancelled reminder succeeds.
    [HttpDelete("reminders/{reminderId:guid}")]
    public Task<IActionResult> RemoveMassReminder(Guid reminderId, CancellationToken ct) =>
        ExecuteAsync(nameof(RemoveMassReminder), async () =>
        {
            var userId = User.GetUserId();
            await _reminderService.RemoveReminderAsync(userId, reminderId, ct);
            var response = ApiResponseBuilder.Build<object?>(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.MassReminderRemoved, null);
            return StatusCode(response.Status, response);
        }, reminderId);
}
