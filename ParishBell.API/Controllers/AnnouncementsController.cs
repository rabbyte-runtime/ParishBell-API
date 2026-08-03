using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/announcements")]
[Authorize]
public class AnnouncementsController(IAnnouncementService announcementService, IMessageCache messages, ILogger<AnnouncementsController> logger) : ApiControllerBase(logger)
{
    private readonly IAnnouncementService _announcementService = announcementService;
    private readonly IMessageCache _messages = messages;

    // NOTE: GET /api/v1/announcements/{announcementId}
    // IMPORTANT: Requires User JWT and membership of the church's channel - the same gate as the list.
    // IMPORTANT: Lets the client fetch a media URL at the moment it presses play.
    // NOTE: URLs are short-lived SAS tokens, so a list loaded minutes ago is already stale.
    // NOTE: 404 once expired or removed, 403 when the caller does not follow the church.
    [HttpGet("{announcementId:guid}")]
    public Task<IActionResult> GetAnnouncement(
        Guid announcementId,
        [FromHeader(Name = "Accept-Language")] string? acceptLanguage,
        CancellationToken ct) =>
        ExecuteAsync(nameof(GetAnnouncement), async () =>
        {
            var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
            var userId = User.GetUserId();
            var result = await _announcementService.GetAnnouncementAsync(userId, announcementId, languageCode, ct);
            var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.AnnouncementRetrieved, result);
            return StatusCode(response.Status, response);
        }, announcementId);
}
