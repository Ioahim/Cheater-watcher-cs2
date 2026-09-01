using System.Security.Cryptography;
using System.Text;

namespace CheaterWatcher.Api.Services.Auth;

/// <summary>
/// Resolves the JWT signing key. Prefers an explicitly configured secret (env var
/// "Jwt__SecretKey" / the "Jwt:SecretKey" config value). When none is supplied, a random
/// key is generated on first run and persisted to a file so sessions survive restarts.
/// </summary>
public sealed class JwtKeyProvider(IConfiguration configuration, IHostEnvironment env)
{
    private const int MinKeyBytes = 32;

    public string Resolve()
    {
        var configured = configuration["Jwt:SecretKey"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (Encoding.UTF8.GetByteCount(configured) < MinKeyBytes)
                throw new InvalidOperationException(
                    $"Jwt:SecretKey is too short. Provide a key of at least {MinKeyBytes} bytes (or unset it to have one generated automatically).");
            return configured;
        }

        var keyFile = Path.Combine(env.ContentRootPath, "data", "jwt.key");
        Directory.CreateDirectory(Path.GetDirectoryName(keyFile)!);

        if (File.Exists(keyFile))
        {
            var existing = File.ReadAllText(keyFile).Trim();
            if (!string.IsNullOrEmpty(existing))
                return existing;
        }

        var bytes = new byte[MinKeyBytes];
        RandomNumberGenerator.Fill(bytes);
        var generated = Convert.ToBase64String(bytes);

        try
        {
            File.WriteAllText(keyFile, generated, Encoding.UTF8);
        }
        catch (Exception)
        {
            // Non-fatal: fall back to the in-memory key (sessions will not survive restarts).
        }

        return generated;
    }
}
