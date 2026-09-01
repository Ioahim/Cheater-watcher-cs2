using CheaterWatcher.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheaterWatcher.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountMapRank> AccountMapRanks => Set<AccountMapRank>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<PlayerStatsCache> PlayerStatsCache => Set<PlayerStatsCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).HasMaxLength(200);
            e.Property(a => a.Steam64Id).HasMaxLength(32);
            e.HasIndex(a => a.Steam64Id).IsUnique().HasFilter("\"Steam64Id\" IS NOT NULL");
            e.HasOne(a => a.User)
                .WithMany(u => u.Accounts)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).HasMaxLength(100);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.PasswordHash).HasMaxLength(500);
            e.Property(u => u.Steam64Id).HasMaxLength(32);
            e.HasIndex(u => u.Steam64Id).IsUnique().HasFilter("\"Steam64Id\" IS NOT NULL");
        });

        modelBuilder.Entity<AccountMapRank>(e =>
        {
            e.ToTable("account_map_ranks");
            e.HasKey(r => new { r.AccountId, r.Map });
            e.Property(r => r.Map).HasMaxLength(100);
            e.HasOne(r => r.Account)
                .WithMany(a => a.MapRanks)
                .HasForeignKey(r => r.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Match>(e =>
        {
            e.ToTable("matches");
            e.HasKey(m => m.Id);
            e.Property(m => m.MapName).HasMaxLength(100);
            e.Property(m => m.Mode).HasMaxLength(50);
            e.Property(m => m.DemoFileName).HasMaxLength(300);
            e.Property(m => m.DemoSourceId).HasMaxLength(128);
            e.Property(m => m.ErrorMessage).HasMaxLength(2000);
            e.HasOne(m => m.Account)
                .WithMany(a => a.Matches)
                .HasForeignKey(m => m.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.AccountId, m.DemoSourceId }).IsUnique()
                .HasFilter("\"DemoSourceId\" IS NOT NULL");
            e.HasIndex(m => m.FinishedAt);
            e.HasIndex(m => new { m.AccountId, m.Status });
        });

        modelBuilder.Entity<MatchPlayer>(e =>
        {
            e.ToTable("match_players");
            e.HasKey(p => p.Id);
            e.Property(p => p.Steam64Id).HasMaxLength(32);
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.SuspicionBreakdownJson).HasColumnType("jsonb");
            e.HasOne(p => p.Match)
                .WithMany(m => m.Players)
                .HasForeignKey(p => p.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => p.Steam64Id);
            e.HasIndex(p => new { p.MatchId, p.Steam64Id });
        });

        modelBuilder.Entity<PlayerStatsCache>(e =>
        {
            e.ToTable("player_stats_cache");
            e.HasKey(c => c.Steam64Id);
            e.Property(c => c.Steam64Id).HasMaxLength(32);
            e.Property(c => c.PayloadJson).HasColumnType("jsonb");
        });
    }
}
