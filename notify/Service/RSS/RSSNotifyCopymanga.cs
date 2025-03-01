using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notify.Domain.Config;
using Notify.Domain.Models;
using Notify.Domain.Utils;
using Notify.Repository;

namespace Notify.Service.RSS;

public class RSSNotifyCopymanga : RSSNotifyBase 
{
    public RSSNotifyCopymanga(IServiceProvider serviceProvider, ILogger<RSSNotifyCopymanga> logger) : base(serviceProvider, logger)
    {
    }

    /// <summary>
    /// 检查漫画更新并发送消息
    /// </summary>
    /// <param name="sendMsg">是否要发送消息</param>
    /// <returns></returns>
    public async Task CheckMangaUpdateAndSendMessage(bool sendMsg)
    {
        await this.BeginTransaction<NotifyContext>(async notifyContext =>
        {
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var groups = await notifyContext.RSSConfig
                .Where(r => r.SubscribeChannel == Consts.CopymangaUpdateChannel && r.IsActive && now > r.ExpCheckTime)
                .GroupBy(r => r.SubscribeId)
                .ToListAsync();
            foreach (var group in groups)
            {
                var channel = GetMangaChapter(group.Key);
                if (channel == null)
                {
                    logger.LogInformation($"manga not exist: {group.Key}");
                    continue;
                }
                foreach (var record in group) 
                {
                    var lastCheckTime = record.LastCheckTime;
                    record.ExpCheckTime = now + record.CheckInterval;
                    record.LastCheckTime = now;
                    // 只发最新的
                    var latest = channel.Items[0];
                    var date = DateTime.Parse(latest.PubDate);
                    if (new DateTimeOffset(date).ToUnixTimeSeconds() <= lastCheckTime)
                    {
                        continue;
                    }
                    var message = BuildCopymangaUpdateBotMessage(channel);
                    if (message == null)
                    {
                        continue;
                    }
                    logger.LogInformation($"send copymanga update, name:{record.SubscribeId}, message:{JsonSerializer.Serialize(message)}");
                    if (sendMsg)
                    {
                        var success = await oneBotApi.SendMessage(record.MsgTargetId, record.MsgTargetType, message);
                        if (success)
                        {
                            record.LastMsgSendTime = now;
                        }
                    }
                }
            }
        });
    }

    public RSSChannel? GetMangaChapter(string id)
    {
        var url = $"{hubUrl}/copymanga/comic/{id}/0";
        var rssChannel = LoadRSS(url);
        if (rssChannel == null || rssChannel.Items.Count == 0)
        {
            return null;
        }
        return rssChannel;
    }

    public OneBotMessage? BuildCopymangaUpdateBotMessage(RSSChannel rssChannel)
    {
        if (rssChannel == null || rssChannel.Items.Count == 0)
        {
            return null;
        }
        var title = rssChannel.Title.Split("-")[1].Trim();
        var rssItem = rssChannel.Items[0];
        var msg = new OneBotMessage();
        var text = $"漫画⭐{title}⭐更新了{rssItem.Title}\n{rssItem.Link}";
        msg.Items.Add(new OneBotMessageItem
        {
            Type = OneBotMessageType.Text.ToCustomString(),
            Data = new OneBotMessageItemData {
                Text = text,
            }
        });
        return msg;
    }
}