using Microsoft.EntityFrameworkCore;
using Notify.Domain.Models;
using Notify.Repository;
using Notify.Repository.Entity;

namespace Notify.Service.Manager;

public class ChatBotManager
{
    private NotifyContext notifyContext;
    private ILogger<ChatBotManager> logger;

    public ChatBotManager(NotifyContext notifyContext, ILogger<ChatBotManager> logger)
    {
        this.notifyContext = notifyContext;
        this.logger = logger;
    }

    public async Task<ChatConfigDO?> GetChatConfigById(long id)
    {
        return (await notifyContext.ChatConfig.FirstOrDefaultAsync(r => r.Id == id))?.ToChatConfigDO();
    }

    /// <summary>
    /// 获取聊天相关配置
    /// </summary>
    /// <returns></returns>
    public async Task<(List<ChatConfigDO>, int)> GetChatConfig(string? targetId, string? targetType, bool? active, int page, int size)
    {
        if (page == 0)
        {
            page = 1;
        }
        var query = notifyContext.ChatConfig.AsQueryable();
        if (!string.IsNullOrEmpty(targetId))
        {
            query = query.Where(r => r.TargetId == targetId);
        }
        if (active.HasValue)
        {
            query = query.Where(r => r.IsActive == active.Value);
        }
        if (!string.IsNullOrEmpty(targetType))
        {
            query = query.Where(r => r.TargetType == targetType);
        }
        var total = await query.CountAsync();
        query = query.Skip((page - 1) * size).Take(size);
        return (await query.Select(r => r.ToChatConfigDO()).ToListAsync(), total);
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="configs"></param>
    /// <returns></returns>
    public async Task SaveChatConfig(List<ChatConfigDO> configs)
    {
        if (configs.Count == 0)
        {
            return;
        }
        var entities = configs.Select(r => r.ToChatConfigEntity()).ToList();
        foreach (var entity in entities.Where(r => !r.Id.HasValue))
        {
            this.notifyContext.Add(entity);
        }
        foreach (var entity in entities.Where(r => r.Id.HasValue))
        {
            this.notifyContext.Attach(entity);
            var entry = this.notifyContext.Entry(entity);
            entry.Property(r => r.TargetId).IsModified = true;
            entry.Property(r => r.TargetType).IsModified = true;
            entry.Property(r => r.Model).IsModified = true;
            entry.Property(r => r.Provider).IsModified = true;
            entry.Property(r => r.Comment).IsModified = true;
        }
        await this.notifyContext.SaveChangesAsync();
    }

    /// <summary>
    /// 设置状态
    /// </summary>
    /// <param name="id"></param>
    /// <param name="IsActive"></param>
    /// <returns></returns>
    public async Task SetChatConfigStatus(long id, bool isActive)
    {
        var cfg = await this.notifyContext.ChatConfig.Where(r => r.Id == id).FirstOrDefaultAsync();
        if (cfg != null)
        {
            cfg.IsActive = isActive;
            await this.notifyContext.SaveChangesAsync();
        }
    }
}