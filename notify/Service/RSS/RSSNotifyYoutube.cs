
using Microsoft.EntityFrameworkCore;
using Notify.Domain.Config;
using Notify.Domain.Models;
using Notify.Repository;
using System.Text.Json;
using Notify.Domain.Utils;

namespace Notify.Service.RSS;

public class RSSNotifyYoutube : RSSNotifyBase
{
    public RSSNotifyYoutube(IServiceProvider serviceProvider, ILogger<RSSNotifyYoutube> logger) : base(serviceProvider, logger)
    {
    }

    /// <summary>
    /// 检查开播状态并发送消息
    /// </summary>
    /// <param name="sendMsg">是否要发送消息</param>
    /// <returns></returns>
    public async Task CheckLiveStatusAndSendMessage(bool sendMsg)
    {
        await this.BeginTransaction<NotifyContext>(async notifyContext =>
        {
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var groups = await notifyContext.RSSConfig
                .Where(r => r.SubscribeChannel == Consts.YoutubeLiveChannel && r.IsActive && now > r.ExpCheckTime)
                .GroupBy(r => r.SubscribeId)
                .ToListAsync();
            foreach (var group in groups)
            {
                var channel = GetLiveInfo(group.Key);
                if (channel == null)
                {
                    logger.LogInformation($"get youtube live info name:{group.Key}, no update");
                    continue;
                }
                foreach (var record in group) 
                {
                    var latest = channel.Items[0];
                    var date = DateTime.Parse(latest.PubDate);
                    if (new DateTimeOffset(date).ToUnixTimeSeconds() <= record.LastCheckTime)
                    {
                        continue;
                    }
                    record.ExpCheckTime = now + record.CheckInterval;
                    record.LastCheckTime = now;
                    var message = BuildLiveOneBotMessage(channel);
                    if (message == null)
                    {
                        continue;
                    }
                    logger.LogInformation($"send youtube live info name:{record.SubscribeId}, message:{JsonSerializer.Serialize(message)}");
                    if (sendMsg)
                    {
                        var success = await this.oneBotApi.SendMessage(record.MsgTargetId, record.MsgTargetType, message);
                        if (success)
                        {
                            record.LastMsgSendTime = now;
                        }
                    }
                }
            }
        });
    }

    public RSSChannel? GetLiveInfo(string name)
    {
        var url = $"{hubUrl}/youtube/live/{name}/noembed";
        var rssChannel = LoadRSS(url);
        if (rssChannel == null || rssChannel.Items.Count == 0)
        {
            return null;
        }
        return rssChannel;
    }

    public OneBotMessage? BuildLiveOneBotMessage(RSSChannel rssChannel)
    {
        if (rssChannel == null || rssChannel.Items.Count == 0)
        {
            return null;
        }
        var rssItem = rssChannel.Items[0];
        var msg = new OneBotMessage();
        var author = rssChannel.Title.Substring(0, rssChannel.Title.Length - 14);
        var text = $"⭐{author}⭐直播中\n{rssItem.Title}\n{rssItem.Link}";
        msg.Items.Add(new OneBotMessageItem
        {
            Type = OneBotMessageType.Text.ToCustomString(),
            Data = new OneBotMessageItemData {
                Text = text,
            }
        });
        if (!string.IsNullOrEmpty(rssItem.RelatedImg)) 
        {
            msg.Items.Add(new OneBotMessageItem
            {
                Type = OneBotMessageType.CQImage.ToCustomString(),
                Data = new OneBotMessageItemData {
                    Url = rssItem.RelatedImg,
                }
            });
        }
        return msg;
    }
}
