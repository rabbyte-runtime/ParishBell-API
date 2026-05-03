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