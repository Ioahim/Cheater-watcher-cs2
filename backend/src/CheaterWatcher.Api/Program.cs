using System.Text;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Persistence;
using CheaterWatcher.Api.Services;
using CheaterWatcher.Api.Services.Auth;
using CheaterWatcher.Api.Services.Ingestion;
using CheaterWatcher.Api.Services.Leetify;
using CheaterWatcher.Api.Services.Suspicion;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

    // "auth" - tight window on credential endpoints.
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });

    // "upload" - guarded because each request streams up to 600 MB to disk.
    options.AddFixedWindowLimiter("upload", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });

    // "share" - guarded because each request triggers a Valve demo download.
    options.AddFixedWindowLimiter("share", limiter =>
    {
        limiter.PermitLimit = 20;
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
        context => RateLimitPartition.GetFixedWindowLimiter("global_" + (context.User.Identity?.IsAuthenticated == true ? "auth" : "anon"),
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
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

builder.Services.AddSingleton<JwtKeyProvider>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<SteamOpenIdService>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddMemoryCache();

// Persist the DataProtection key ring next to the JWT key so encrypted state
// (and this warning) survives container recreation.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "data")));

builder.Services.AddSingleton<DemoStorage>();
builder.Services.AddSingleton<DemoExtractor>();
builder.Services.AddSingleton<ParseQueue>();
builder.Services.AddScoped<ShareCodeIngestionService>();
builder.Services.AddSingleton<ISuspicionScorer, RuleBasedSuspicionScorer>();
builder.Services.AddScoped<LeetifyService>();

var leetify = builder.Configuration.GetSection("Leetify").Get<LeetifyOptions>() ?? new LeetifyOptions();
builder.Services.AddHttpClient<LeetifyClient>(http => http.BaseAddress = new Uri(leetify.BaseUrl.TrimEnd('/') + "/"));
builder.Services.AddHttpClient<SteamWebApiClient>(http => http.BaseAddress = new Uri("https://api.steampowered.com/"));
builder.Services.AddHttpClient<DemoDownloader>();

builder.Services.AddHostedService<ParseWorker>();
if (!string.IsNullOrWhiteSpace(builder.Configuration["Steam:WebApiKey"]))
    builder.Services.AddHostedService<ShareCodePollingWorker>();
builder.Services.AddHostedService<PlayerStatsCachePurger>();

const string frontendPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendPolicy, policy =>
    {
        var origins = builder.Configuration["CorsOrigins"] ?? "";
        if (string.IsNullOrWhiteSpace(origins) || origins == "*")
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        policy.AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = new JwtKeyProvider(builder.Configuration, builder.Environment).Resolve();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = signingKey
        };
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
        await DbSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database migration/seeding failed - API will keep running but DB endpoints will error until the database is reachable.");
    }
}

app.UseCors(frontendPolicy);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
