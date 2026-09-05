using System.Text.Json;
using System.Text.Json.Serialization;

namespace CheaterWatcher.Api.Services.Leetify;

public class LeetifyOptions
{
    public string BaseUrl { get; set; } = "https://api-public.cs-prod.leetify.com";
    public string ApiKey { get; set; } = string.Empty;
    public int CacheHours { get; set; } = 24;
}

public sealed record PlatformBan(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("platform_nickname")] string? PlatformNickname,
    [property: JsonPropertyName("banned_since")] DateTime? BannedSince);

public sealed record LeetifyStats(
    [property: JsonPropertyName("preaim")] double? Preaim,
    [property: JsonPropertyName("reaction_time_ms")] double? ReactionTimeMs,
    [property: JsonPropertyName("accuracy_head")] double? AccuracyHead,
    [property: JsonPropertyName("spray_accuracy")] double? SprayAccuracy,
    [property: JsonPropertyName("counter_strafing_good_shots_ratio")] double? CounterStrafingGoodShotsRatio);

public sealed record LeetifyProfile(
    [property: JsonPropertyName("privacy_mode")] string? PrivacyMode,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("bans")] List<PlatformBan>? Bans,
    [property: JsonPropertyName("stats")] LeetifyStats? Stats)
{
    public bool IsPublic => string.Equals(PrivacyMode, "public", StringComparison.OrdinalIgnoreCase);
}

public class LeetifyClient
{
    private readonly HttpClient _http;
    private readonly LeetifyOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public LeetifyClient(HttpClient http, Microsoft.Extensions.Options.IOptions<LeetifyOptions> options)
    {
        _http = http;
        _options = options.Value;
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Add("_leetify_key", _options.ApiKey);
    }

    public async Task<LeetifyProfile?> GetProfileAsync(string steam64Id, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"v3/profile?steam64_id={Uri.EscapeDataString(steam64Id)}", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<LeetifyProfile>(stream, JsonOptions, ct);
    }
}
