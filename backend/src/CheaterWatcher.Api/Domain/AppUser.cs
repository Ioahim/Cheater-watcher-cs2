namespace CheaterWatcher.Api.Domain;

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Steam64Id { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Account> Accounts { get; set; } = [];
}
