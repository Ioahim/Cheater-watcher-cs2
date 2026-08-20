using Microsoft.EntityFrameworkCore;

namespace CheaterWatcher.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
