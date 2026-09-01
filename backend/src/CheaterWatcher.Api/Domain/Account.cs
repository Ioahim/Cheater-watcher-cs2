namespace CheaterWatcher.Api.Domain;

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? PremierRating { get; set; }
    public int? WingmanLevel { get; set; }
    public string? Steam64Id { get; set; }
    public string? AuthCode { get; set; }
    public string? LatestShareCode { get; set; }
    public int? UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<AccountMapRank> MapRanks { get; set; } = [];
    public ICollection<Match> Matches { get; set; } = [];
}
