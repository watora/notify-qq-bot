using Microsoft.EntityFrameworkCore;
using Notify.Repository;
using Notify.Domain.Models;

namespace Notify.Service.Manager;

public class EntityConfigManager
{
    private NotifyContext notifyContext;

    public EntityConfigManager(NotifyContext notifyContext)
    {
        this.notifyContext = notifyContext;
    }

    public async Task<EntityConfigDO?> GetRSSConfigByKey(string key)
    {
        var cfg = await notifyContext.KVConfig.FirstOrDefaultAsync(r => r.Key == key);
        if (cfg == null)
        {
            return null;
        }
        return new EntityConfigDO
        {
            Key = cfg.Key,
            Value = cfg.Value,
            Creator = cfg.Creator
        };
    }

    /// <summary>
    /// 根据条件查询配置
    /// </summary>
    /// <param name="page"></param>
    /// <param name="size"></param>
    /// <param name="key"></param>
    /// <param name="active"></param>
    /// <returns></returns>
    public async Task<(List<EntityConfigDO>, int)> GetEntityConfig(int page, int size, string? key)
    {
        var query = notifyContext.KVConfig.AsQueryable();
        if (!string.IsNullOrEmpty(key))
        {
            query = query.Where(r => r.Key == key);
        }
        var total = await query.CountAsync();
        var configs = await query.Skip((page - 1) * size).Take(size).ToListAsync();
        return (configs.Select(r => new EntityConfigDO
        {
            Key = r.Key,
            Value = r.Value,
            Creator = r.Creator
        }).ToList(), total);
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="configs"></param>
    /// <returns></returns>
    public async Task SaveEntityConfig(List<EntityConfigDO> configs, string creator)
    {
        if (configs.Count == 0)
        {
            return;
        }
        var keys = configs.Select(r => r.Key);
        var entities = await notifyContext.KVConfig.Where(r => keys.Contains(r.Key)).ToDictionaryAsync(r => r.Key, r => r);
        foreach (var config in configs)
        {
            if (entities.ContainsKey(config.Key!))
            {
                entities[config.Key!].Value = config.Value!;
                entities[config.Key!].Comment = config.Comment ?? "";
            }
            else
            {
                notifyContext.KVConfig.Add(new Repository.Entity.KVConfig
                {
                    Key = config.Key!,
                    Value = config.Value!,
                    Creator = creator,
                    Comment = config.Comment ?? "",
                });
            }
        }
        await notifyContext.SaveChangesAsync();
    }

    /// <summary>
    /// 删除配置
    /// </summary>
    /// <returns></returns>
    public async Task DeleteEntityConfig(string key)
    {
        var cfg = await notifyContext.KVConfig.Where(r => r.Key == key).FirstOrDefaultAsync();
        if (cfg != null)
        {
            notifyContext.Remove(cfg);
            await notifyContext.SaveChangesAsync();
        }
    }
}