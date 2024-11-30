using Microsoft.EntityFrameworkCore;

namespace Notify.Repository;

public class NotifyContext : DbContext
{
    private string dbPath;
    public NotifyContext(IConfiguration configuration)
    {
        this.dbPath = configuration["Db:Path"] ?? "";
    }

    public DbSet<Entity.RSSConfig> RSSConfig { get; set; }
    public DbSet<Entity.ChatConfig> ChatConfig { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }
}