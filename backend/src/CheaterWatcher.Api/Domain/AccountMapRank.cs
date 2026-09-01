namespace CheaterWatcher.Api.Domain;

public class AccountMapRank
{
    public int AccountId { get; set; }
    public string Map { get; set; } = string.Empty;
    public int Level { get; set; }
    public Account Account { get; set; } = null!;
}
