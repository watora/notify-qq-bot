using Microsoft.Extensions.Primitives;
using Notify.Repository;

namespace Notify.Utils;

public class EntityConfigurationProvider : ConfigurationProvider
{
    private EntityConfigurationSource source;

    public EntityConfigurationProvider(EntityConfigurationSource source) 
    {
        this.source = source;
        if (source.GetReloadToken() != null) 
        {
            ChangeToken.OnChange(source.GetReloadToken, Load);
        }
    }

    public override void Load()
    {
        using var notifyContext = new NotifyContext(source.ConnectionString);
        Data = notifyContext.KVConfig.AsQueryable().ToDictionary(r => r.Key, r => (string?)r.Value, StringComparer.OrdinalIgnoreCase);
    }
}