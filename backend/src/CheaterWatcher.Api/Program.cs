using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Services;
using CheaterWatcher.Api.Services.Auth;
using CheaterWatcher.Api.Services.Ingestion;
using CheaterWatcher.Api.Services.Leetify;
using CheaterWatcher.Api.Services.Suspicion;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 600_000_000;
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // "upload" - guarded because each request streams up to 600 MB to disk.
    options.AddFixedWindowLimiter("upload", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });

    // "external" - guarded because each request fans out to third-party APIs (Steam/Leetify).
    options.AddFixedWindowLimiter("external", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });

    // Baseline guard for endpoints that didn't declare a policy.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => RateLimitPartition.GetFixedWindowLimiter("global_anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["POSTGRES_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(connectionString))
{
    var dbPassword = builder.Configuration["POSTGRES_PASSWORD"];
    if (!string.IsNullOrWhiteSpace(dbPassword))
        connectionString = connectionString.Replace("{POSTGRES_PASSWORD}", dbPassword);
    else if (connectionString.Contains("{POSTGRES_PASSWORD}"))
        connectionString = null;
}
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Database connection is not configured. Set a full ConnectionStrings:DefaultConnection (or POSTGRES_CONNECTION_STRING), or provide POSTGRES_PASSWORD to fill the default connection string.");
    return;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<LeetifyOptions>(builder.Configuration.GetSection("Leetify"));
builder.Services.Configure<SuspicionOptions>(builder.Configuration.GetSection("Suspicion"));
builder.Services.Configure<SteamOptions>(builder.Configuration.GetSection("Steam"));
builder.Services.Configure<OpenIdOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<ReplayScanOptions>(builder.Configuration.GetSection("Replays"));

builder.Services.AddSingleton<SteamOpenIdService>();
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<DemoStorage>();
builder.Services.AddSingleton<DemoExtractor>();
builder.Services.AddSingleton<DemoInfoReader>();
builder.Services.AddSingleton<ParseQueue>();
builder.Services.AddSingleton<ScoreQueue>();
builder.Services.AddSingleton<BanCheckQueue>();
builder.Services.AddSingleton<ISuspicionScorer, RuleBasedSuspicionScorer>();
builder.Services.AddScoped<LeetifyService>();
builder.Services.AddScoped<SteamPlayerBanService>();
builder.Services.AddScoped<MatchSuspicionService>();
builder.Services.AddScoped<ReplayProcessor>();
builder.Services.AddScoped<RankIngest>();
builder.Services.AddScoped<PendingReplayResolver>();
builder.Services.AddScoped<ReplayEnvService>();
builder.Services.AddSingleton<ReplayScanner>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ReplayScanner>());

var leetify = builder.Configuration.GetSection("Leetify").Get<LeetifyOptions>() ?? new LeetifyOptions();
builder.Services.AddHttpClient<LeetifyClient>(http => http.BaseAddress = new Uri(leetify.BaseUrl.TrimEnd('/') + "/"));
builder.Services.AddHttpClient<SteamWebApiClient>(http =>
{
    http.BaseAddress = new Uri("https://api.steampowered.com/");
    http.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("steam-openid", c => c.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddHostedService<ParseWorker>();
builder.Services.AddHostedService<ScoreWorker>();
builder.Services.AddHostedService<BanCheckWorker>();
builder.Services.AddHostedService<PlayerStatsCachePurger>();

const string frontendPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendPolicy, policy =>
    {
        var origins = builder.Configuration["CorsOrigins"] ?? "";
        if (string.IsNullOrWhiteSpace(origins) || origins == "*")
        {
            policy.WithOrigins("http://localhost:3000");
        }
        else
        {
            policy.WithOrigins(origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        policy.AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database migration failed - API will keep running but DB endpoints will error until the database is reachable.");
    }
}

app.UseCors(frontendPolicy);
app.UseRateLimiter();

app.MapControllers();

app.Run();
