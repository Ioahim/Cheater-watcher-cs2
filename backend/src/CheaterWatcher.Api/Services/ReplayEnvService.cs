using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services;

/// <summary>
/// Reads and writes app settings in the repo .env file (bind-mounted into the
/// container). The app collects the user's replays path and Steam Web API key once,
/// persists them here, and a restart of the stack (docker compose up -d) re-creates
/// the containers with the new values.
/// </summary>
public class ReplayEnvService(IOptions<ReplayScanOptions> options, ILogger<ReplayEnvService> logger)
{
    private const string ReplaysKey = "STEAM_REPLAYS_ROOT";
    private const string SteamKey = "STEAM_WEB_API_KEY";
    private string? _startupEnvPath;
    private readonly object _lock = new();

    public bool CanWriteEnv => File.Exists(options.Value.HostEnvPath);

    /// <summary>
    /// The STEAM_REPLAYS_ROOT value that was active when this container started
    /// (captured on first access). Used to tell the user whether a restart is needed
    /// for a newly-saved path to take effect.
    /// </summary>
    public string StartupEnvPath
    {
        get
        {
            if (_startupEnvPath is null)
            {
                lock (_lock)
                {
                    _startupEnvPath ??= ReadCurrentEnvPath();
                }
            }
            return _startupEnvPath;
        }
    }

    /// <summary>
    /// The STEAM_REPLAYS_ROOT value currently in the .env file. Falls back to the
    /// runtime environment variable, then the default.
    /// </summary>
    public string ReadCurrentEnvPath()
    {
        try
        {
            var envPath = options.Value.HostEnvPath;
            if (File.Exists(envPath))
            {
                var value = ParseLine(File.ReadAllLines(envPath), ReplaysKey);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read {EnvPath}", options.Value.HostEnvPath);
        }

        return Environment.GetEnvironmentVariable(ReplaysKey) ?? "./replays";
    }

    /// <summary>
    /// The STEAM_WEB_API_KEY value currently in the .env file, or null when absent.
    /// </summary>
    public string? ReadSteamApiKey()
    {
        try
        {
            var envPath = options.Value.HostEnvPath;
            if (File.Exists(envPath))
            {
                var value = ParseLine(File.ReadAllLines(envPath), SteamKey);
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read {EnvPath}", options.Value.HostEnvPath);
        }

        return null;
    }

    /// <summary>
    /// Persists the given path as STEAM_REPLAYS_ROOT in the .env file, preserving all
    /// other lines and comments. Returns false if the file is not mounted/accessible.
    /// </summary>
    public bool WriteEnvPath(string path)
    {
        var envPath = options.Value.HostEnvPath;
        if (!File.Exists(envPath))
        {
            logger.LogWarning("Cannot persist replays path: {EnvPath} not found", envPath);
            return false;
        }

        try
        {
            var lines = File.ReadAllLines(envPath).ToList();
            // Docker Compose on Windows mis-parses "\" escapes (e.g. "\r") in .env, so
            // persist the path with forward slashes.
            var value = path.Trim(' ', '"').Replace('\\', '/');
            UpsertLine(lines, ReplaysKey, QuoteIfNeeded(value));

            File.WriteAllLines(envPath, lines);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not write {EnvPath}", envPath);
            return false;
        }
    }

    /// <summary>
    /// Persists the given key as STEAM_WEB_API_KEY in the .env file, preserving all other
    /// lines and comments. Returns false if the file is not mounted/accessible.
    /// </summary>
    public bool WriteSteamApiKey(string apiKey)
    {
        var envPath = options.Value.HostEnvPath;
        if (!File.Exists(envPath))
        {
            logger.LogWarning("Cannot persist Steam API key: {EnvPath} not found", envPath);
            return false;
        }

        try
        {
            var lines = File.ReadAllLines(envPath).ToList();
            UpsertLine(lines, SteamKey, apiKey.Trim());

            File.WriteAllLines(envPath, lines);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not write {EnvPath}", envPath);
            return false;
        }
    }

    private static void UpsertLine(List<string> lines, string key, string value)
    {
        var idx = lines.FindIndex(l => l.TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            lines[idx] = $"{key}={value}";
        }
        else
        {
            lines.Add($"{key}={value}");
            lines.Add(string.Empty);
        }
    }

    private static string QuoteIfNeeded(string value)
    {
        // Paths with spaces must be quoted for docker-compose to parse them.
        return value.Contains(' ') && !value.StartsWith('"') ? $"\"{value}\"" : value;
    }

    private static string? ParseLine(IEnumerable<string> lines, string key)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed[(key.Length + 1)..].Trim();
                return value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"')
                    ? value[1..^1]
                    : value;
            }
        }
        return null;
    }
}