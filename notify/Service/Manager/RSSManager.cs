using Microsoft.EntityFrameworkCore;
using Notify.Repository;
using Notify.Domain.Models;
using Notify.Utils;

namespace Notify.Service.Manager;

public class RSSManager
{
    private NotifyContext notifyContext;

    public RSSManager(NotifyContext notifyContext)
    {
        this.notifyContext = notifyContext;
    }

    public async Task<RSSConfigDO?> GetRSSConfigById(long id)
    {
        return (await notifyContext.RSSConfig.FirstOrDefaultAsync(r => r.Id == id))?.ToRSSConfigDO();
    }

    /// <summary>
    /// 根据条件查询配置
    /// </summary>
    /// <param name="page"></param>
    /// <param name="size"></param>
    /// <param name="subscribeChannel"></param>
    /// <param name="active"></param>
    /// <returns></returns>
    public async Task<(List<RSSConfigDO>, int)> GetRSSConfig(int page, int size, string subscribeChannel, bool? active)
    {
        var query = notifyContext.RSSConfig.Where(r => r.SubscribeChannel == subscribeChannel);
        if (active != null)
        {
            query = query.Where(r => r.IsActive == active);
        }
        var total = await query.CountAsync();
        var configs = await query.Skip((page - 1) * size).Take(size).ToListAsync();
        return (configs.Select(r => r.ToRSSConfigDO()).ToList(), total);
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="configs"></param>
    /// <returns></returns>
    public async Task SaveRSSConfig(List<RSSConfigDO> configs)
    {
        if (configs.Count == 0)
        {
            return;
        }
        var entities = configs.Select(r => r.ToRSSConfigEntity()).ToList();
        foreach (var entity in entities.Where(r => !r.Id.HasValue))
        {
            entity.LastCheckTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            notifyContext.Add(entity);
        }
        foreach (var entity in entities.Where(r => r.Id.HasValue))
        {
            notifyContext.Attach(entity);
            var entry = notifyContext.Entry(entity);
            entry.Property(r => r.MsgTargetId).IsModified = true;
            entry.Property(r => r.MsgTargetType).IsModified = true;
            entry.Property(r => r.SubscribeId).IsModified = true;
            entry.Property(r => r.SubscribeChannel).IsModified = true;
            entry.Property(r => r.CheckInterval).IsModified = true;
            entry.Property(r => r.Comment).IsModified = true;
        }
        await notifyContext.SaveChangesAsync();
    }

    /// <summary>
    /// 设置状态
    /// </summary>
    /// <param name="id"></param>
    /// <param name="IsActive"></param>
    /// <returns></returns>
    public async Task SetRSSConfigStatus(long id, bool isActive)
    {
        var cfg = await notifyContext.RSSConfig.Where(r => r.Id == id).FirstOrDefaultAsync();
        if (cfg != null)
        {
            cfg.IsActive = isActive;
            await notifyContext.SaveChangesAsync();
        }
    }
}