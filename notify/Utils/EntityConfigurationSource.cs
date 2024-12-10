using Microsoft.Extensions.Primitives;

namespace Notify.Utils;

public class EntityConfigurationSource : IConfigurationSource
{
    public string ConnectionString { get; set; }
    private ConfigurationReloadToken changeToken;


    public EntityConfigurationSource(string dbPath)
    {
        ConnectionString = dbPath;
        changeToken = new ConfigurationReloadToken();
        var timer = new System.Timers.Timer(TimeSpan.FromMinutes(5));
        timer.Elapsed += (sender, e) =>
        {
            Interlocked.Exchange(ref changeToken, new ConfigurationReloadToken())?.OnReload();
        };
    }

    public IChangeToken GetReloadToken()
    {
        return changeToken;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new EntityConfigurationProvider(this);
    }
}