namespace CheaterWatcher.Api.Services.Auth;

public sealed class OpenIdOptions
{
    public string FrontendBaseUrl { get; init; } = "http://localhost:3000";
    public string ApiBaseUrl { get; init; } = "http://localhost:5089";
}
