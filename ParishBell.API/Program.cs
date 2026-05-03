using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParishBell.API.Middleware;
using ParishBell.Application.Services;
using ParishBell.Core.Configuration;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.BackgroundJobs;
using ParishBell.Infrastructure.Caching;
using ParishBell.Infrastructure.Data;
using ParishBell.Infrastructure.Repositories;
using ParishBell.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// NOTE: Add OpenApi support
builder.Services.AddOpenApi();

// NOTE: Add controllers
builder.Services.AddControllers();

// NOTE: Register the DbContext
builder.Services.AddDbContext<ParishBellDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// NOTE: Add Memory Cache
builder.Services.AddMemoryCache();

// NOTE: Inject IMessageRepository and IMessageCache
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IMessageCache, MessageCache>();

// NOTE: Add hosted services
builder.Services.AddHostedService<MessageCacheStartupService>();
builder.Services.AddHostedService<MessageCacheRefreshJob>();

// NOTE: Service registrations
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// NOTE: Load user secrets
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddUserSecrets<Program>();

// NOTE: Rate limiting - 10 requests per IP address per minute on auth endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // NOTE: Custom 429 response when rate limit is hit
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync("""{"status":429,"messageCode":"PB-16","messageType":"Error","message":"Too many requests. Please slow down and try again."}""", ct);
    };
});

// NOTE: Custom model validation error response
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        // NOTE: Read Accept-Language from headers
        var lang = context.HttpContext.Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',')[0].Trim().ToLowerInvariant();
        lang = lang is "en" or "si" or "ta" ? lang : "en";

        // NOTE: Resolve IMessageCache from DI
        var messages = context.HttpContext.RequestServices.GetRequiredService<IMessageCache>();

        // NOTE: Build field errors with localized messages
        var fieldErrors = context.ModelState.Where(kvp => kvp.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kvp => ToCamelCase(kvp.Key),
                            kvp => kvp.Value!.Errors
                                .Select(e => new
                                {
                                    messageCode = e.ErrorMessage,
                                    message = messages.GetText(e.ErrorMessage, lang),
                                }).ToArray());

        var response = new
        {
            status = 422,
            messageCode = "PB-25",  // NOTE: Generic model validation error message code
            messageType = "Error",
            message = messages.GetText("PB-25", lang),
            fieldErrors,
            traceId = context.HttpContext.TraceIdentifier,
        };

        return new UnprocessableEntityObjectResult(response)
        {
            ContentTypes = { "application/json" }
        };
    };
});

var app = builder.Build();

// NOTE: Global exception handler
app.UseMiddleware<GlobalExceptionMiddleware>();

// NOTE: OpenApi on development server
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// NOTE: Redirect HTTP requests to HTTPS
app.UseHttpsRedirection();

// NOTE: Enable rate limiting
app.UseRateLimiter();

// NOTE: Map controller endpoints
app.MapControllers();

app.Run();

// NOTE: Helper method to convert to CamelCase
static string ToCamelCase(string name) => string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];