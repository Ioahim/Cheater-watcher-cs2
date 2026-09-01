using System.Text.Json;
using System.Text.Json.Serialization;

namespace CheaterWatcher.Api.Services.Ingestion;

public class SteamOptions
{
    public string WebApiKey { get; set; } = string.Empty;
    public int PollingIntervalSeconds { get; set; } = 60;
}

public sealed record SteamPlayerSummary(string? PersonaName, string? AvatarFull);

public sealed record PlayerSummariesResult([property: JsonPropertyName("response")] PlayerSummariesPayload? Response);

public sealed record PlayerSummariesPayload([property: JsonPropertyName("players")] List<PlayerSummaryItem>? Players);

public sealed record PlayerSummaryItem(
    [property: JsonPropertyName("steamid")] string? SteamId,
    [property: JsonPropertyName("personaname")] string? PersonaName,
    [property: JsonPropertyName("avatarfull")] string? AvatarFull);

public sealed record NextMatchSharingCodeResult(
    [property: JsonPropertyName("result")] NextMatchSharingCodePayload? Result);

public sealed record NextMatchSharingCodePayload(
    [property: JsonPropertyName("sharing_code")] string? SharingCode);

/// <summary>Outcome of a single GetNextMatchSharingCode call.</summary>
public enum ShareCodePollResult
{
    /// <summary>No new match code (204/202, or a 200 with empty/"n/a" sharing code).</summary>
    NoData,

    /// <summary>A decodable next match sharing code.</summary>
    Ok,

    /// <summary>Steam rejected the knowncode (412 invalid/too-old) or the auth (401/403 revoked);
    /// polling should stop until the user supplies a fresh code / regenerates auth.</summary>
    NeedsAttention,

    /// <summary>Any other transient failure.</summary>
    Error,
}

public sealed record ShareCodePollOutcome(ShareCodePollResult Result, string? SharingCode = null);

public class SteamWebApiClient
{
    private readonly HttpClient _http;
    private readonly SteamOptions _options;

    public SteamWebApiClient(HttpClient http, Microsoft.Extensions.Options.IOptions<SteamOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<SteamPlayerSummary?> GetPlayerSummariesAsync(string steam64Id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebApiKey))
            return null;

        var url = $"ISteamUser/GetPlayerSummaries/v2/" +
                  $"?key={Uri.EscapeDataString(_options.WebApiKey)}" +
                  $"&steamids={Uri.EscapeDataString(steam64Id)}";

        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<PlayerSummariesResult>(cancellationToken: ct);
            var player = payload?.Response?.Players?.FirstOrDefault(p => p.SteamId == steam64Id);
            if (player is null)
                return null;

            return new SteamPlayerSummary(player.PersonaName, player.AvatarFull);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ShareCodePollOutcome> GetNextMatchSharingCodeAsync(string steam64Id, string authCode, string knownCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebApiKey))
            return new ShareCodePollOutcome(ShareCodePollResult.Error);

        var url = $"ICSGOPlayers_730/GetNextMatchSharingCode/v1/" +
                  $"?key={Uri.EscapeDataString(_options.WebApiKey)}" +
                  $"&steamid={Uri.EscapeDataString(steam64Id)}" +
                  $"&steamidkey={Uri.EscapeDataString(authCode)}" +
                  $"&knowncode={Uri.EscapeDataString(knownCode)}";

        using var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.StatusCode == System.Net.HttpStatusCode.Accepted)
            return new ShareCodePollOutcome(ShareCodePollResult.NoData);

        if (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed ||     // 412: knowncode invalid / too old
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||           // 401
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)                // 403: auth revoked
            return new ShareCodePollOutcome(ShareCodePollResult.NeedsAttention);

        if (!response.IsSuccessStatusCode)
            return new ShareCodePollOutcome(ShareCodePollResult.Error);

        var payload = await response.Content.ReadFromJsonAsync<NextMatchSharingCodeResult>(cancellationToken: ct);
        var code = payload?.Result?.SharingCode;
        return string.IsNullOrWhiteSpace(code) || code == "n/a"
            ? new ShareCodePollOutcome(ShareCodePollResult.NoData)
            : new ShareCodePollOutcome(ShareCodePollResult.Ok, code);
    }
}
