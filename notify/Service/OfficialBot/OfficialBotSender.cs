using System.Text;
using Microsoft.Extensions.Options;
using Notify.Domain.Config;
using Notify.Domain.Config.Options;
using Notify.Domain.Models;

namespace Notify.Service.OfficialBot;

/// <summary>将现有业务生成的文本/图片分段通过官方 API 发送。</summary>
public class OfficialBotSender
{
    private readonly OfficialBotApi api;
    private readonly OfficialBotOption options;
    private readonly ILogger<OfficialBotSender> logger;

    public OfficialBotSender(OfficialBotApi api, IOptions<OfficialBotOption> options, ILogger<OfficialBotSender> logger)
    {
        this.api = api;
        this.options = options.Value;
        this.logger = logger;
    }

    public string? GetMappedTarget(string oneBotId, string messageType)
    {
        var mappings = GetMappings(messageType);
        if (mappings.TryGetValue(oneBotId, out var openId) && !string.IsNullOrWhiteSpace(openId))
            return openId;
        logger.LogWarning("No official OpenID mapping for OneBot {Type} target {Target}; configure OfficialBot target mappings",
            messageType, oneBotId);
        return null;
    }

    /// <summary>目标使用官方 OpenID，或已配置映射的旧 QQ 号/群号；作为主动消息发送。</summary>
    public async Task<bool> SendMessage(string id, string messageType, OneBotMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(message);
        var mappings = GetMappings(messageType);
        if (mappings.TryGetValue(id, out var mapped))
        {
            if (string.IsNullOrWhiteSpace(mapped))
                return false;
            id = mapped;
        }
        if (!options.Enabled)
        {
            logger.LogWarning("Official bot is disabled; message was not sent");
            return false;
        }

        // Validate every segment before making requests, keeping the original text/image order.
        var parts = new List<(string? Text, string? ImageUrl)>();
        var text = new StringBuilder();
        void FlushText()
        {
            if (!string.IsNullOrWhiteSpace(text.ToString()))
                parts.Add((text.ToString(), null));
            text.Clear();
        }
        foreach (var item in message.Items)
        {
            switch (item.Type)
            {
                case "text":
                    text.Append(item.Data.Text);
                    break;
                case "reply":
                    // OneBot message IDs cannot be used as official msg_id/message_reference.
                    break;
                case "image":
                    FlushText();
                    var url = string.IsNullOrWhiteSpace(item.Data.Url) ? item.Data.File : item.Data.Url;
                    if (url?.StartsWith("//", StringComparison.Ordinal) == true)
                        url = "https:" + url;
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                        throw new ArgumentException("Official bot image segments require a public HTTP/HTTPS URL.", nameof(message));
                    parts.Add((null, url));
                    break;
                default:
                    throw new NotSupportedException($"Unsupported outgoing message segment: {item.Type}");
            }
        }
        FlushText();
        if (parts.Count == 0)
            return false;

        // Prepare all media before sending text so an upload failure cannot silently drop images.
        var messages = new List<OfficialBotMessage>();
        foreach (var part in parts)
        {
            if (part.ImageUrl == null)
                messages.Add(new OfficialBotMessage { Content = part.Text });
            else
            {
                var fileInfo = await api.UploadImage(id, messageType, part.ImageUrl, cancellationToken);
                if (fileInfo == null)
                    return false;
                messages.Add(new OfficialBotMessage { MsgType = 7, Content = " ", Media = new OfficialBotMedia { FileInfo = fileInfo } });
            }
        }
        foreach (var outgoing in messages)
        {
            if (!await api.SendMessage(id, messageType, outgoing, cancellationToken))
                return false;
        }
        return true;
    }

    private Dictionary<string, string> GetMappings(string messageType) => messageType switch
    {
        Consts.MsgTargetTypePrivate => options.PrivateTargetMappings,
        Consts.MsgTargetTypeGroup => options.GroupTargetMappings,
        _ => throw new ArgumentException("Message type must be private or group.", nameof(messageType))
    };
}
