namespace CheaterWatcher.Api.Domain;

public class ProcessedReplay
{
    public long Id { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public DateTime ProcessedAt { get; set; }
}
