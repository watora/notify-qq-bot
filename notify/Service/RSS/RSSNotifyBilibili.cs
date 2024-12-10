using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Notify.Domain.Config;
using Notify.Repository;
using Notify.Domain.Models;
using Notify.Domain.Utils;

namespace Notify.Service.RSS;

public class RSSNotifyBilibili : RSSNotifyBase
{
    public RSSNotifyBilibili(IServiceProvider serviceProvider, ILogger<RSSNotifyBilibili> logger) : base(serviceProvider, logger)
    {
    }

    /// <summary>
    /// 检查新动态并发送消息
    /// </summary>
    /// <param name="sendMsg">是否要发送消息</param>
    /// <returns></returns>
    public async Task CheckNewDynamicAndSendMessage(bool sendMsg)
    {
        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        await this.BeginTransaction<NotifyContext>(async notifyContext =>
        {
            var group = await notifyContext.RSSConfig
                .Where(r => r.SubscribeChannel == Consts.BilibiliDynamicChannel && r.IsActive && now > r.ExpCheckTime)
                .GroupBy(r => r.SubscribeId)
                .ToListAsync();
            foreach (var records in group)
            {
                var channel = GetNewDynamic(records.Key);
                if (channel == null)
                {
                    logger.LogInformation($"get bilibili new dynamic userId:{records.Key}, no update");
                    continue;
                }
                foreach (var record in records)
                {
                    var lastCheckTime = record.LastCheckTime;
                    record.LastCheckTime = now;
                    record.ExpCheckTime = now + record.CheckInterval;
                    if (!sendMsg)
                    {
                        continue;
                    }
                    var validItems = new List<RSSItem>();
                    foreach (var item in channel.Items)
                    {
                        var date = DateTime.Parse(item.PubDate);
                        if (new DateTimeOffset(date).ToUnixTimeSeconds() > lastCheckTime)
                        {
                            validItems.Add(item);
                        }
                    }
                    var messages = BuildDynamicOneBotMessage(validItems);
                    foreach (var message in messages)
                    {
                        logger.LogInformation($"send bilibili new dynamic userId:{record.SubscribeId}, message:{JsonSerializer.Serialize(message)}");
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
            var group = await notifyContext.RSSConfig
                .Where(r => r.SubscribeChannel == Consts.BilibiliLiveChannel && r.IsActive && now > r.ExpCheckTime)
                .GroupBy(r => r.SubscribeId)
                .ToListAsync();
            foreach (var records in group)
            {
                var channel = GetLiveInfo(records.Key);
                if (channel == null)
                {
                    logger.LogInformation($"get bilibili live info roomId:{records.Key}, no update");
                    continue;
                }
                foreach (var record in records)
                {
                    var lastCheckTime = record.LastCheckTime;
                    record.ExpCheckTime = now + record.CheckInterval;
                    record.LastCheckTime = now;
                    if (!sendMsg)
                    {
                        continue;
                    }
                    var latest = channel.Items[0];
                    var date = DateTime.Parse(latest.PubDate);
                    if (new DateTimeOffset(date).ToUnixTimeSeconds() <= lastCheckTime)
                    {
                        continue;
                    }
                    var message = BuildLiveOneBotMessage(channel);
                    if (message == null)
                    {
                        continue;
                    }
                    logger.LogInformation($"send bilibili live info roomId:{record.SubscribeId}, message:{JsonSerializer.Serialize(message)}");
                    var success = await this.oneBotApi.SendMessage(record.MsgTargetId, record.MsgTargetType, message);
                    if (success)
                    {
                        record.LastMsgSendTime = now;
                    }
                }


            }
        });
    }

    /// <summary>
    /// 获取新增动态
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    public RSSChannel? GetNewDynamic(string uid)
    {
        var url = $"{hubUrl}/bilibili/user/dynamic/{uid}";
        var channel = LoadRSS(url);
        if (channel == null || channel.Items.Count == 0)
        {
            return null;
        }
        return channel;
    }

    /// <summary>
    /// 获取直播信息
    /// </summary>
    /// <returns></returns>
    public RSSChannel? GetLiveInfo(string roomId)
    {
        var url = $"{hubUrl}/bilibili/live/room/{roomId}";
        var rssChannel = LoadRSS(url);
        if (rssChannel == null || rssChannel.Items.Count == 0)
        {
            return null;
        }
        return rssChannel;
    }


    /// <summary>
    /// 动态转换成onebot消息
    /// </summary>
    /// <returns></returns>
    public List<OneBotMessage> BuildDynamicOneBotMessage(List<RSSItem> validItems)
    {
        var messages = new List<OneBotMessage>();
        foreach (var rssItem in validItems)
        {
            var msg = new OneBotMessage();
            var title = $"⭐{rssItem.Author}⭐发布了新动态\n{rssItem.Link}\n";
            var replaced = Regex.Replace(rssItem.Description, "(<br>)+", " ");
            if (!replaced.Contains(rssItem.Title.TrimEnd('.')))
            {
                title += rssItem.Title;
            }
            msg.Items.Add(new OneBotMessageItem
            {
                Type = OneBotMessageType.Text.ToCustomString(),
                Data = new OneBotMessageItemData
                {
                    Text = title,
                }
            });
            var desc = rssItem.Description.Replace("(<br>)+", "\n");
            var doc = ParseHtml(desc);
            foreach (var child in doc.ChildNodes)
            {
                switch (child.Name)
                {
                    case "img":
                        var src = child.Attributes["src"];
                        if (src == null)
                        {
                            continue;
                        }
                        msg.Items.Add(new OneBotMessageItem
                        {
                            Type = OneBotMessageType.CQImage.ToCustomString(),
                            Data = new OneBotMessageItemData { Url = src.Value }
                        });
                        break;
                    case "a":
                        var href = child.Attributes["href"].Value;
                        msg.Items.Add(new OneBotMessageItem
                        {
                            Type = OneBotMessageType.Text.ToCustomString(),
                            Data = new OneBotMessageItemData { Text = href }
                        });
                        break;
                    case "#text":
                        msg.Items.Add(new OneBotMessageItem
                        {
                            Type = OneBotMessageType.Text.ToCustomString(),
                            Data = new OneBotMessageItemData
                            {
                                Text = child.InnerHtml,
                            }
                        });
                        break;
                    default:
                        continue;
                }
            }
            messages.Add(msg);
        }
        return messages;
    }

    /// <summary>
    /// 直播状态转成onebot消息
    /// </summary>
    /// <param name="rssChannel"></param>
    /// <returns></returns>
    private OneBotMessage? BuildLiveOneBotMessage(RSSChannel rssChannel)
    {
        if (rssChannel == null || rssChannel.Items.Count == 0)
        {
            return null;
        }
        var rssItem = rssChannel.Items[0];
        var msg = new OneBotMessage();
        var title = rssItem.Title.Substring(0, rssItem.Title.Length - 19);
        var author = rssChannel.Title.Split(" ")[0];
        var text = $"⭐{author}⭐直播中\n{title}\n{rssItem.Link}";
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