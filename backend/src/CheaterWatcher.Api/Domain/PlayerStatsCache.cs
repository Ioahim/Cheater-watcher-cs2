namespace CheaterWatcher.Api.Domain;

public class PlayerStatsCache
{
    public string Steam64Id { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; }
}
