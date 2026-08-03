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
    // IMPORTANT: Requires User JWT. One month across the churches the caller follows.
    // NOTE: Following nothing returns an empty list, not a 404.
    // NOTE: Optional ?month= and ?year= filter to one month, defaulting to the current.
    // NOTE: Mirrors /locations/followed/events so the client can merge both onto one grid.
    // NOTE: Weekly masses come back already expanded onto dates.
    // NOTE: Each occurrence carries the caller reminder, or null.
    // NOTE: No second call is needed to render the bells.
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
    // IMPORTANT: Requires User JWT. Always sets the reminder for the token own user.
    // NOTE: Idempotent upsert keyed by (user, schedule), so posting again re-times it.
    // NOTE: It also switches a disabled reminder back on rather than failing.
    // NOTE: Returns the saved reminder so the calendar rebinds without re-fetching.
    // NOTE: 404 when the mass is gone or hidden, 422 when minutesBefore is outside 1-1440.
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
    // IMPORTANT: Requires User JWT. Another user reminder id returns 404, not 403.
    // IMPORTANT: Cancels rather than erases - the row is kept switched off.
    // NOTE: The schedule endpoint reports isActive:false, and POSTing again revives it.
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
