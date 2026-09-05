namespace CheaterWatcher.Api.Services;

public class ReplayScanOptions
{
    // Root directory inside the container that the host Steam replays folder is
    // bind-mounted to (see docker-compose.yml STEAM_REPLAYS_ROOT).
    public string RootPath { get; set; } = "/app/replays-root";

    // Path to the repo .env file (bind-mounted rw) so the first-time path entered
    // in the app can be persisted as STEAM_REPLAYS_ROOT for the next compose up.
    public string HostEnvPath { get; set; } = "/run/replays.env";

    public int DefaultScanIntervalMinutes { get; set; } = 20;
}
