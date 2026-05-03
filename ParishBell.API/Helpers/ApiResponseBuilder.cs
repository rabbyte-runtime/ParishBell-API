using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Helpers;

public static class ApiResponseBuilder
{
    public static ParishBellApiResponse<T> Build<T>(
        HttpContext context,
        IMessageCache messages,
        int status,
        string messageCode,
        T? responseData = default)
    {
        var lang = GetLanguageCode(context);

        return new ParishBellApiResponse<T>
        {
            Status = status,
            MessageCode = messageCode,
            MessageType = messages.GetType(messageCode).ToString(),
            Message = messages.GetText(messageCode, lang),
            TraceId = context.TraceIdentifier,
            ResponseData = responseData,
        };
    }

    public static string GetLanguageCode(HttpContext context)
    {
        var lang = context.Request.Headers.AcceptLanguage
            .FirstOrDefault()
            ?.Split(',')[0]
            .Trim()
            .ToLowerInvariant();

        return lang is "en" or "si" or "ta" ? lang : "en";
    }
}