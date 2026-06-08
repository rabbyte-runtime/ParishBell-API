using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/languages")]
public class LanguagesController(ILanguageService languageService, IMessageCache messages) : ControllerBase
{
    private readonly ILanguageService _languageService = languageService;
    private readonly IMessageCache _messages = messages;

    // NOTE: GET /api/v1/languages
    // IMPORTANT: Public - the client needs this on the sign-up screen before it has a JWT.
    [HttpGet]
    public async Task<IActionResult> GetLanguages([FromHeader(Name = "Accept-Language")] string? acceptLanguage, CancellationToken ct)
    {
        // NOTE: Default to English when blank header
        var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
        var result = await _languageService.GetActiveLanguagesAsync(languageCode, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LanguagesRetrieved, result);
        return StatusCode(StatusCodes.Status200OK, response);
    }
}