using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services.Auth;

public sealed class SteamOpenIdService(IHttpClientFactory httpClientFactory, IOptions<AuthOptions> options)
{
    private const string OpenIdEndpoint = "https://steamcommunity.com/openid/login";

    public string BuildLoginUrl(string state)
    {
        var auth = options.Value;
        var returnUrl =
            $"{auth.ApiBaseUrl.TrimEnd('/')}/api/auth/steam/callback?state={Uri.EscapeDataString(state)}";
        var query = new Dictionary<string, string>
        {
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = returnUrl,
            ["openid.realm"] = auth.ApiBaseUrl.TrimEnd('/'),
            ["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select",
            ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select",
        };

        return OpenIdEndpoint + "?" + string.Join("&",
            query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }

    /// <summary>
    /// Verifies the OpenID callback parameters against Steam (check_authentication)
    /// and returns the claimed 17-digit Steam64 id, or null when verification fails.
    /// </summary>
    public async Task<string?> VerifyCallbackAsync(IQueryCollection query, CancellationToken ct)
    {
        if (query["openid.mode"] != "id_res")
            return null;

        // Defend against a callback with a mismatched return_to (association/replay
        // hardening). The value must point back at the exact callback we issued.
        var expectedBase = $"{options.Value.ApiBaseUrl.TrimEnd('/')}/api/auth/steam/callback";
        var returnTo = query["openid.return_to"].ToString();
        if (!returnTo.StartsWith(expectedBase, StringComparison.OrdinalIgnoreCase))
            return null;

        var fields = query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        fields["openid.mode"] = "check_authentication";

        using var client = httpClientFactory.CreateClient("steam-openid");
        using var response = await client.PostAsync(OpenIdEndpoint, new FormUrlEncodedContent(fields), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!body.Contains("is_valid:true", StringComparison.OrdinalIgnoreCase))
            return null;

        var claimedId = query["openid.claimed_id"].ToString();
        var lastSegment = claimedId.Split('/').LastOrDefault();
        return lastSegment is { Length: 17 } steam64 && steam64.All(char.IsDigit) ? steam64 : null;
    }
}
