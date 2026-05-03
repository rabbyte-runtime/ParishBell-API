using System.Text.Json;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.API.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task InvokeAsync(HttpContext context, IMessageCache messages)
    {
        try
        {
            await _next(context);
        }
        catch (ParishBellException ex)
        {
            _logger.LogWarning("ParishBellException on {Method} {Path}: {Code}", context.Request.Method, context.Request.Path, ex.MessageCode);
            await WriteResponseAsync(context, ex.StatusCode, ex.MessageCode, messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteResponseAsync(context, 500, MessageCodes.GeneralUnexpectedError, messages);
        }
    }

    private static async Task WriteResponseAsync(HttpContext context, int statusCode, string messageCode, IMessageCache messages)
    {
        var lang = context.Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',')[0].Trim().ToLowerInvariant();
        lang = lang is "en" or "si" or "ta" ? lang : "en";

        var response = new ParishBellApiResponse<object>
        {
            Status = statusCode,
            MessageCode = messageCode,
            MessageType = messages.GetType(messageCode).ToString(),
            Message = messages.GetText(messageCode, lang),
            TraceId = context.TraceIdentifier,
            ResponseData = null,
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static string GetLanguageCode(HttpContext context)
    {
        var lang = context.Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',')[0].Trim().ToLowerInvariant();
        return lang is "en" or "si" or "ta" ? lang : "en";
    }
}