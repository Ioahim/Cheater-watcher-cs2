namespace CheaterWatcher.Api.Domain;

public class ReplayScanSettings
{
    public int Id { get; set; } = 1;
    public string HostPath { get; set; } = string.Empty;
    public DateTime? LastScanAt { get; set; }
    public int LastScanNew { get; set; }
    public int LastScanAttributed { get; set; }
    public int LastScanPending { get; set; }
    public string? LastScanError { get; set; }
}
