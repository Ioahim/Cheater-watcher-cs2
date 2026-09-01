namespace CheaterWatcher.Api.Domain;

public class MatchPlayer
{
    public long Id { get; set; }
    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;
    public string Steam64Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TeamNumber { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public double? SuspicionScore { get; set; }
    public string? SuspicionBreakdownJson { get; set; }
    public short? RankType { get; set; }
    public int? RankValue { get; set; }
    public DateTime? FlaggedAt { get; set; }
    public int FlagReason { get; set; }
    public string? FlagNote { get; set; }
}
