using Microsoft.AspNetCore.Mvc;
using ParishBell.API.Helpers;
using ParishBell.Core.Constants;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Controllers;

[ApiController]
[Route("api/v1/location-types")]
public class LocationTypesController(ILocationTypeService locationTypeService, IMessageCache messages) : ControllerBase
{
    private readonly ILocationTypeService _locationTypeService = locationTypeService;
    private readonly IMessageCache _messages = messages;

    // GET /api/v1/location-types
    // IMPORTANT: do not gate this endpoint - the map filter and onboarding screens need this list.
    [HttpGet]
    public async Task<IActionResult> GetLocationTypes([FromHeader(Name = "Accept-Language")] string? acceptLanguage, CancellationToken ct)
    {
        var languageCode = string.IsNullOrWhiteSpace(acceptLanguage) ? "en" : acceptLanguage.Trim();
        var result = await _locationTypeService.GetActiveLocationTypesAsync(languageCode, ct);
        var response = ApiResponseBuilder.Build(HttpContext, _messages, StatusCodes.Status200OK, MessageCodes.LanguagesRetrieved, result);
        return StatusCode(response.Status, response);
    }
}