using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.BackgroundJobs;
using ParishBell.Infrastructure.Caching;
using ParishBell.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// NOTE: Add OpenApi support
builder.Services.AddOpenApi();

// NOTE: Add Memory Cache
builder.Services.AddMemoryCache();

// NOTE: Inject IMessageRepository and IMessageCache
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IMessageCache, MessageCache>();

// NOTE: Add hosted services
builder.Services.AddHostedService<MessageCacheStartupService>();
builder.Services.AddHostedService<MessageCacheRefreshJob>();

var app = builder.Build();

// NOTE: Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.Run();