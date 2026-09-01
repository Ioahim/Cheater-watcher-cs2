namespace CheaterWatcher.Api.Services.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 60;
}

public sealed class AuthOptions
{
    public string FrontendBaseUrl { get; init; } = "http://localhost:3000";
    public string ApiBaseUrl { get; init; } = "http://localhost:5089";
}
