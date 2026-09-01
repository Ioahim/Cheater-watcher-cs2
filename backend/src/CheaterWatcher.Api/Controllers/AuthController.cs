using CheaterWatcher.Api.Contracts;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Infrastructure;
using CheaterWatcher.Api.Services.Auth;
using CheaterWatcher.Api.Services.Ingestion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    TokenService tokens,
    IPasswordHasher<AppUser> hasher,
    SteamOpenIdService openId,
    SteamWebApiClient steam,
    IMemoryCache cache,
    IOptions<AuthOptions> authOptions) : ControllerBase
{
    private const string LinkStatePrefix = "auth:link:";
    private const string SteamCodePrefix = "auth:steam:";

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        if (username.Length is < 3 or > 32 || !username.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
            return BadRequest(new { error = "Username must be 3-32 characters (letters, digits, _ or -)." });
        if (request.Password.Length is < 8 or > 128)
            return BadRequest(new { error = "Password must be 8-128 characters." });

        var taken = await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct);
        if (taken)
            return Conflict(new { error = "Username is already taken." });

        var user = new AppUser
        {
            Username = username,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "Username is already taken." });
        }

        return Ok(await ToAuthResponseAsync(user, ct));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), ct);
        if (user is null)
            return Unauthorized(new { error = "Invalid username or password." });

        var verified = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verified == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Invalid username or password." });

        return Ok(await ToAuthResponseAsync(user, ct));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserDto>> Me(CancellationToken ct)
    {
        if (User.TryGetUserId() is not { } userId)
            return Unauthorized();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return Unauthorized();

        return Ok(await ToDtoAsync(user, ct));
    }

    [Authorize]
    [HttpGet("steam/link")]
    public IActionResult LinkSteam()
    {
        if (User.TryGetUserId() is not { } userId)
            return Unauthorized();

        var state = Guid.NewGuid().ToString("N");
        cache.Set(LinkStatePrefix + state, userId, TimeSpan.FromMinutes(15));
        return Ok(new { url = openId.BuildLoginUrl(state) });
    }

    [HttpGet("steam/callback")]
    public async Task<IActionResult> SteamCallback([FromQuery] string? state, CancellationToken ct)
    {
        var frontend = authOptions.Value.FrontendBaseUrl.TrimEnd('/');
        if (string.IsNullOrEmpty(state) ||
            !cache.TryGetValue(LinkStatePrefix + state, out int userId))
            return Redirect($"{frontend}/stats#steam=expired");

        cache.Remove(LinkStatePrefix + state);

        var steam64 = await openId.VerifyCallbackAsync(Request.Query, ct);
        if (steam64 is null)
            return Redirect($"{frontend}/stats#steam=failed");

        var code = Guid.NewGuid().ToString("N");
        cache.Set(SteamCodePrefix + code, (UserId: userId, Steam64: steam64), TimeSpan.FromMinutes(10));
        return Redirect($"{frontend}/stats#steam_code={code}");
    }

    [Authorize]
    [HttpPost("steam/exchange")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthUserDto>> ExchangeSteamCode(SteamExchangeRequest request, CancellationToken ct)
    {
        if (User.TryGetUserId() is not { } userId)
            return Unauthorized();

        if (!cache.TryGetValue(SteamCodePrefix + request.Code, out (int UserId, string Steam64) entry))
            return BadRequest(new { error = "Invalid or expired Steam link code." });
        cache.Remove(SteamCodePrefix + request.Code);

        if (entry.UserId != userId)
            return BadRequest(new { error = "This code belongs to a different session." });

        var alreadyLinked =
            await db.Users.AnyAsync(u => u.Id != userId && u.Steam64Id == entry.Steam64, ct);
        if (alreadyLinked)
            return Conflict(new { error = "This Steam account is linked to another user." });

        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        user.Steam64Id = entry.Steam64;

        // Best-effort enrichment with Steam persona name + avatar (no key / failure is non-fatal).
        var summary = await steam.GetPlayerSummariesAsync(entry.Steam64, ct);
        if (!string.IsNullOrWhiteSpace(summary?.AvatarFull))
            user.AvatarUrl = summary.AvatarFull;

        // Claim or create the user's own tracked account so parsing/ranks attach automatically.
        var ownAccount = await db.Accounts.FirstOrDefaultAsync(a => a.Steam64Id == entry.Steam64, ct);
        if (ownAccount is null)
        {
            db.Accounts.Add(new Account
            {
                Name = !string.IsNullOrWhiteSpace(summary?.PersonaName) ? summary.PersonaName! : user.Username,
                Steam64Id = entry.Steam64,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            ownAccount.UserId ??= userId;
            if (!string.IsNullOrWhiteSpace(summary?.PersonaName))
                ownAccount.Name = summary.PersonaName!;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "This Steam account is linked to another user." });
        }

        return Ok(await ToDtoAsync(user, ct));
    }

    private async Task<AuthResponse> ToAuthResponseAsync(AppUser user, CancellationToken ct) =>
        new(tokens.Issue(user), await ToDtoAsync(user, ct));

    private async Task<AuthUserDto> ToDtoAsync(AppUser user, CancellationToken ct)
    {
        int? ownAccountId = null;
        string? personaName = null;
        if (user.Steam64Id is not null)
        {
            var own = await db.Accounts.AsNoTracking()
                .Where(a => a.UserId == user.Id && a.Steam64Id == user.Steam64Id)
                .OrderBy(a => a.Id)
                .Select(a => new { a.Id, a.Name })
                .FirstOrDefaultAsync(ct);
            ownAccountId = own?.Id;
            personaName = own?.Name;
        }

        return new AuthUserDto(user.Id, user.Username, user.Steam64Id, user.AvatarUrl, ownAccountId, personaName);
    }
}
