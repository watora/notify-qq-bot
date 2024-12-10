using Microsoft.EntityFrameworkCore;

namespace Notify.Repository;

public class NotifyContext : DbContext
{
    private string dbPath;

    [ActivatorUtilitiesConstructor]
    public NotifyContext(IConfiguration configuration)
    {
        this.dbPath = configuration["Db:Path"] ?? "";
    }

    public NotifyContext(string dbPath)
    {
        this.dbPath = dbPath;
    }

    public DbSet<Entity.RSSConfig> RSSConfig { get; set; }
    public DbSet<Entity.ChatConfig> ChatConfig { get; set; }
    public DbSet<Entity.KVConfig> KVConfig { get; set; }



    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }
}