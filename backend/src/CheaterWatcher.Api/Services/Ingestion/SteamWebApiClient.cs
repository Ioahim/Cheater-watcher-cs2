using System.Text.Json;
using System.Text.Json.Serialization;

namespace CheaterWatcher.Api.Services.Ingestion;

public class SteamOptions
{
    public string WebApiKey { get; set; } = string.Empty;
    public int BanCacheHours { get; set; } = 48;
}

public sealed record SteamPlayerSummary(string? PersonaName, string? AvatarFull);

public sealed record PlayerSummariesResult([property: JsonPropertyName("response")] PlayerSummariesPayload? Response);

public sealed record PlayerSummariesPayload([property: JsonPropertyName("players")] List<PlayerSummaryItem>? Players);

public sealed record PlayerSummaryItem(
    [property: JsonPropertyName("steamid")] string? SteamId,
    [property: JsonPropertyName("personaname")] string? PersonaName,
    [property: JsonPropertyName("avatarfull")] string? AvatarFull);

public sealed record SteamPlayerBan(
    [property: JsonPropertyName("SteamId")] string SteamId,
    [property: JsonPropertyName("CommunityBanned")] bool CommunityBanned,
    [property: JsonPropertyName("VACBanned")] bool VacBanned,
    [property: JsonPropertyName("NumberOfVACBans")] int NumberOfVACBans,
    [property: JsonPropertyName("NumberOfGameBans")] int NumberOfGameBans,
    [property: JsonPropertyName("DaysSinceLastBan")] int DaysSinceLastBan,
    [property: JsonPropertyName("EconomyBan")] string EconomyBan);

public sealed record PlayerBansResult([property: JsonPropertyName("players")] List<SteamPlayerBan>? Players);

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

    public async Task<SteamPlayerBan?> GetPlayerBansAsync(string steam64Id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebApiKey))
            return null;

        var url = "ISteamUser/GetPlayerBans/v1/" +
                  $"?key={Uri.EscapeDataString(_options.WebApiKey)}" +
                  $"&steamids={Uri.EscapeDataString(steam64Id)}";

        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<PlayerBansResult>(cancellationToken: ct);
            return payload?.Players?.FirstOrDefault(p => p.SteamId == steam64Id);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
