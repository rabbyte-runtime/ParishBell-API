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
    // IMPORTANT: Exists so the client can fetch a media URL at the moment it presses play. The URLs are short-lived SAS
    // IMPORTANT:  tokens, so one taken from a list loaded twenty minutes ago will already have expired mid-session.
    // NOTE: 404 once the post has expired or been removed, 403 when the caller does not follow that church.
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
