using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/events")]
[Authorize]
public class EventsController(IEventService eventService, IMessageCache messages) : ControllerBase
{
    private readonly IEventService _eventService = eventService;
    private readonly IMessageCache _messages = messages;

    // NOTE: GET /api/v1/events/{eventId}
    // IMPORTANT: Requires User JWT - viewing a shared event means signing in first (§5.8). Not scoped to followers, so any signed-in user can open a shared link.
    // NOTE: The entry point for deep links: a push carrying eventId, and parishbell://events/{eventId}. Everything the detail screen needs, including the hosting church.
    // NOTE: 404 when the event is unpublished, soft-deleted, or its church is no longer visible - a link outliving its event is expected, not exceptional.
    [HttpGet("{eventId:guid}")]
    public async Task<IActionResult> GetEvent(
        Guid eventId,
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        CancellationToken ct)
    {
        var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
        var result = await _eventService.GetEventByIdAsync(eventId, languageCode, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.EventDetailRetrieved, result);
        return StatusCode(response.Status, response);
    }
}
