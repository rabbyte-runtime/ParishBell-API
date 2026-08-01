using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ParishBell.API.Middleware;
using ParishBell.Application.Services;
using ParishBell.Core.Configuration;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.BackgroundJobs;
using ParishBell.Infrastructure.Caching;
using ParishBell.Infrastructure.Data;
using ParishBell.Infrastructure.Email;
using ParishBell.Infrastructure.Push;
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

// NOTE: Configure Google Auth settings - reads from user-secrets
builder.Services.Configure<GoogleAuthSettings>(builder.Configuration.GetSection("GoogleAuth"));

// NOTE: Configure password reset and SendGrid settings
builder.Services.Configure<SendGridSettings>(builder.Configuration.GetSection("SendGrid"));

// NOTE: Configure password reset settings
builder.Services.Configure<PasswordResetSettings>(builder.Configuration.GetSection("PasswordReset"));

// NOTE: Inject IMessageRepository and IMessageCache
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IMessageCache, MessageCache>();

// NOTE: Register email service
builder.Services.AddScoped<IEmailService, SendGridEmailService>();

// NOTE: Firebase Cloud Messaging (push notifications) — credentials come from user-secrets
builder.Services.Configure<FcmSettings>(builder.Configuration.GetSection("Fcm"));
builder.Services.AddSingleton<FirebaseAppInitializer>();
builder.Services.AddScoped<IPushNotificationService, FcmPushNotificationService>();

// NOTE: Announcement push fan-out — background outbox over notifications_log. Announcements are
//       authored in the admin backend (shared DB); this job notifies each location's followers.
var announcementPushSettings = builder.Configuration.GetSection("AnnouncementPush").Get<AnnouncementPushSettings>()
    ?? new AnnouncementPushSettings();
builder.Services.AddSingleton(announcementPushSettings);
builder.Services.AddScoped<IAnnouncementNotificationRepository, AnnouncementNotificationRepository>();
builder.Services.AddScoped<IAnnouncementNotificationService, AnnouncementNotificationService>();
builder.Services.AddHostedService<AnnouncementPushJob>();

// NOTE: Add hosted services
builder.Services.AddHostedService<MessageCacheStartupService>();
builder.Services.AddHostedService<MessageCacheRefreshJob>();

// NOTE: Service registrations
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// NOTE: JWT bearer authentication — validates tokens issued by GenerateAccessToken
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JwtSettings section is missing from configuration.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            // NOTE: Zero clock skew — access tokens are short-lived (15 min), no grace period needed
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IExternalAuthValidator, GoogleAuthValidator>();
builder.Services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
builder.Services.AddScoped<ILanguageRepository, LanguageRepository>();
builder.Services.AddScoped<ILanguageService, LanguageService>();
builder.Services.AddScoped<ILocationTypeRepository, LocationTypeRepository>();
builder.Services.AddScoped<ILocationTypeService, LocationTypeService>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<ILocationFollowRepository, LocationFollowRepository>();
builder.Services.AddScoped<ILocationFollowService, LocationFollowService>();
builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserNotificationRepository, UserNotificationRepository>();
builder.Services.AddScoped<IUserNotificationService, UserNotificationService>();
builder.Services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
builder.Services.AddScoped<IUserDeviceService, UserDeviceService>();
builder.Services.AddScoped<ILiturgicalCalendarRepository, LiturgicalCalendarRepository>();
builder.Services.AddScoped<ILiturgicalCalendarService, LiturgicalCalendarService>();

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
        var lang = context.HttpContext.Request.Headers.AcceptLanguage
            .FirstOrDefault()?.Split(',')[0].Trim().ToLowerInvariant();
        lang = lang is "en" or "si" or "ta" ? lang : "en";

        var messages = context.HttpContext.RequestServices
            .GetRequiredService<IMessageCache>();

        var fieldErrors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => ToCamelCase(kvp.Key),
                kvp => kvp.Value!.Errors
                    .Select(e => new
                    {
                        messageCode = e.ErrorMessage,
                        message = messages.GetText(e.ErrorMessage, lang),
                    }).ToArray());

        var response = new ParishBellApiResponse<object>
        {
            Status = 422,
            MessageCode = "PB-25",
            MessageType = messages.GetType("PB-25").ToString(),
            Message = messages.GetText("PB-25", lang),
            TraceId = context.HttpContext.TraceIdentifier,
            ResponseData = new { fieldErrors },
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

// NOTE: Authentication must come before Authorization in the pipeline
app.UseAuthentication();
app.UseAuthorization();

// NOTE: Enable rate limiting
app.UseRateLimiter();

// NOTE: Map controller endpoints
app.MapControllers();

app.Run();

// NOTE: Helper method to convert to CamelCase
static string ToCamelCase(string name) => string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];