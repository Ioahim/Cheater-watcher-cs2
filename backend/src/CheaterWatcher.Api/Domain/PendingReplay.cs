namespace CheaterWatcher.Api.Domain;

public enum PendingReplayStatus
{
    Pending = 1,
    Resolved = 2,
    Dismissed = 3,
}

public class PendingReplay
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public string MapName { get; set; } = string.Empty;
    public string Mode { get; set; } = "Unknown";
    public DateTime DiscoveredAt { get; set; }
    public PendingReplayStatus Status { get; set; } = PendingReplayStatus.Pending;
    public int? ResolvedAccountId { get; set; }
    public Account? ResolvedAccount { get; set; }
    public string PlayerSteamIdsJson { get; set; } = "[]";
    public string PlayerNamesJson { get; set; } = "[]";
}
