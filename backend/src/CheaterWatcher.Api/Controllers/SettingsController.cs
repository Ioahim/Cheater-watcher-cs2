using CheaterWatcher.Api.Contracts;
using CheaterWatcher.Api.Services;
using CheaterWatcher.Api.Services.Ingestion;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(
    ReplayEnvService env,
    SteamWebApiClient steam,
    IOptions<SteamOptions> steamOptions) : ControllerBase
{
    [HttpGet("steam-key")]
    public ActionResult<SteamKeyStatusDto> GetSteamKeyStatus()
    {
        var active = steamOptions.Value.WebApiKey;
        var fileKey = env.ReadSteamApiKey();
        var configured = !string.IsNullOrWhiteSpace(fileKey);

        return Ok(new SteamKeyStatusDto(
            configured,
            !string.IsNullOrWhiteSpace(active),
            configured && fileKey is { Length: > 0 }
                ? "…" + fileKey[^Math.Min(4, fileKey.Length)..]
                : null,
            configured && !string.Equals(active?.Trim(), fileKey!.Trim(), StringComparison.Ordinal),
            env.CanWriteEnv));
    }

    [HttpPut("steam-key")]
    [EnableRateLimiting("external")]
    public async Task<ActionResult<SaveSteamKeyResult>> SaveSteamKey(
        UpdateSteamKeyRequest request,
        CancellationToken ct)
    {
        var key = request.Key?.Trim();
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "Provide a Steam Web API key." });

        var check = await steam.CheckKeyAsync(key, ct);
        if (check == SteamKeyCheck.Invalid)
            return BadRequest(new { error = "Steam says this is not a valid API key. Double-check it and try again." });

        var active = steamOptions.Value.WebApiKey;
        if (!env.WriteSteamApiKey(key))
            return Ok(new SaveSteamKeyResult(false, check != SteamKeyCheck.Invalid, check == SteamKeyCheck.Valid, false, env.CanWriteEnv));

        var restartRequired = !string.Equals(active?.Trim(), key, StringComparison.Ordinal);
        return Ok(new SaveSteamKeyResult(true, check != SteamKeyCheck.Invalid, check == SteamKeyCheck.Valid, restartRequired, true));
    }
}