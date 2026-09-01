using System.Security.Claims;
using CheaterWatcher.Api.Controllers;
using CheaterWatcher.Api.Contracts;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services.Auth;
using CheaterWatcher.Api.Services.Ingestion;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CheaterWatcher.Tests;

public class AuthTests
{
    private static JwtOptions JwtOpts => new()
    {
        Issuer = "Test",
        Audience = "Test",
        SecretKey = "YLJJqAOs3jW5SY9r/ILizvzRWcvOIMFTWmebd5O/y1I=",
        AccessTokenMinutes = 60,
    };

    private static AuthOptions AuthOpts => new()
    {
        FrontendBaseUrl = "http://localhost:3000",
        ApiBaseUrl = "http://localhost:5089",
    };

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"auth_tests_{name}_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts);
    }

    private static TokenService Tokens() => new(
        Options.Create(JwtOpts),
        new JwtKeyProvider(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = JwtOpts.SecretKey,
            }).Build(),
            new FakeHostEnvironment()));

    private static IMemoryCache Cache() => new MemoryCache(new MemoryCacheOptions());

    private static AuthController CreateController(
        AppDbContext db,
        IMemoryCache? cache = null,
        int? authenticatedUserId = null)
    {
        var controller = new AuthController(
            db,
            Tokens(),
            new PasswordHasher<AppUser>(),
            new SteamOpenIdService(
                new FakeHttpClientFactory(),
                Options.Create(AuthOpts)),
            new SteamWebApiClient(new HttpClient(), Options.Create(new SteamOptions())),
            cache ?? Cache(),
            Options.Create(AuthOpts));

        if (authenticatedUserId is { } userId)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, "testuser"),
            };
            controller.ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims)),
                },
            };
        }
        else
        {
            controller.ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext(),
            };
        }

        return controller;
    }

    [Fact]
    public async Task Register_and_login_returns_token_with_correct_claims()
    {
        using var db = NewDb();
        var ctrl = CreateController(db);

        var regResult = await ctrl.Register(new("testuser1", "password123"), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(regResult.Result);
        var resp = Assert.IsType<AuthResponse>(ok.Value);
        Assert.False(string.IsNullOrEmpty(resp.Token));
        Assert.Equal("testuser1", resp.User.Username);

        var loginResult = await ctrl.Login(new("testuser1", "password123"), CancellationToken.None);
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loginResp = Assert.IsType<AuthResponse>(loginOk.Value);
        Assert.False(string.IsNullOrEmpty(loginResp.Token));
    }

    [Fact]
    public async Task Register_duplicate_username_returns_409()
    {
        using var db = NewDb();
        var ctrl = CreateController(db);

        await ctrl.Register(new("dupe_user", "password123"), CancellationToken.None);
        var result = await ctrl.Register(new("dupe_user", "otherpass123"), CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(conflict.Value);
        Assert.Contains("already taken", json);
    }

    [Fact]
    public async Task Login_wrong_password_returns_401()
    {
        using var db = NewDb();
        var ctrl = CreateController(db);
        await ctrl.Register(new("loginuser", "password123"), CancellationToken.None);

        var result = await ctrl.Login(new("loginuser", "wrongpassword"), CancellationToken.None);
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Me_returns_current_user()
    {
        using var db = NewDb();
        var ctrl = CreateController(db);
        var regResult = await ctrl.Register(new("meuser", "password123"), CancellationToken.None);
        var regOk = Assert.IsType<OkObjectResult>(regResult.Result);
        var regResp = Assert.IsType<AuthResponse>(regOk.Value);

        var meCtrl = CreateController(db, authenticatedUserId: regResp.User.Id);
        var meResult = await meCtrl.Me(CancellationToken.None);
        var meOk = Assert.IsType<OkObjectResult>(meResult.Result);
        var meDto = Assert.IsType<AuthUserDto>(meOk.Value);
        Assert.Equal("meuser", meDto.Username);
    }

    [Fact]
    public async Task Steam_exchange_links_steam64_and_creates_account()
    {
        using var db = NewDb();
        var cache = Cache();
        var ctrl = CreateController(db);
        var regResult = await ctrl.Register(new("steamuser", "password123"), CancellationToken.None);
        var regOk = Assert.IsType<OkObjectResult>(regResult.Result);
        var regResp = Assert.IsType<AuthResponse>(regOk.Value);

        // Simulate a pending Steam code in cache
        var code = "abc123";
        cache.Set($"auth:steam:{code}", (regResp.User.Id, Steam64: "76561198000000000"), TimeSpan.FromMinutes(10));

        var exchCtrl = CreateController(db, cache: cache, authenticatedUserId: regResp.User.Id);
        var result = await exchCtrl.ExchangeSteamCode(new(code), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AuthUserDto>(ok.Value);
        Assert.Equal("76561198000000000", dto.Steam64Id);
        Assert.NotNull(dto.OwnAccountId);
    }

    [Fact]
    public async Task Credential_update_rejects_unowned_account()
    {
        using var db = NewDb();

        var account = new Account { Name = "Mock", CreatedAt = DateTime.UtcNow };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var controller = new AccountsController(
            db,
            // Share code ingestion isn't exercised by this test (no share code, unowned account).
            new ShareCodeIngestionService(null!, null!, null!),
            new SteamWebApiClient(new HttpClient(), Options.Create(new SteamOptions())))
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "999")])),
                },
            },
        };

        var result = await controller.UpdateCredentials(account.Id, new(null, "authcode"), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    private class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "CheaterWatcher.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetTempPath());
    }
}
