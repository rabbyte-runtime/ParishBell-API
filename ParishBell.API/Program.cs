using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ParishBell.Application.Services;
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
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

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

var app = builder.Build();

// NOTE: Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
    app.MapOpenApi();
}

// NOTE: Redirect HTTP requests to HTTPS
app.UseHttpsRedirection();

// NOTE: Map controller endpoints
app.MapControllers();

app.Run();