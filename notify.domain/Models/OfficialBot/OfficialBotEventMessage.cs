using System.Text.Json;
using System.Text.Json.Serialization;
using Notify.Domain.Config;

namespace Notify.Domain.Models;

public class OfficialBotPayload
{
    [JsonPropertyName("op")]
    public required int Op { get; set; }
    [JsonPropertyName("t")]
    public string? Type { get; set; }
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("d")]
    public JsonElement Data { get; set; }
}

public class OfficialBotEventMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    [JsonPropertyName("group_openid")]
    public string? GroupOpenId { get; set; }
    [JsonPropertyName("author")]
    public OfficialBotAuthor? Author { get; set; }
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
    [JsonPropertyName("msg_seq")]
    public int? MsgSeq { get; set; }
    [JsonPropertyName("message_scene")]
    public OfficialBotMessageScene? MessageScene { get; set; }
    [JsonPropertyName("attachments")]
    public List<OfficialBotAttachment>? Attachments { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public string EventType { get; set; } = "";
    [JsonIgnore]
    public string TargetType => EventType == "C2C_MESSAGE_CREATE" ? Consts.MsgTargetTypePrivate : Consts.MsgTargetTypeGroup;
    [JsonIgnore]
    public string TargetId => TargetType == Consts.MsgTargetTypePrivate ? Author?.UserOpenId ?? "" : GroupOpenId ?? "";
    [JsonIgnore]
    public string DeduplicationKey => JsonSerializer.Serialize(new object?[]
    {
        TargetType, TargetId, Id, MsgSeq,
        MessageScene?.Ext?.FirstOrDefault(x => x != null && x.StartsWith("msg_idx=", StringComparison.Ordinal))
    });

    public static bool IsMessageEvent(string? type) =>
        type is "C2C_MESSAGE_CREATE" or "GROUP_AT_MESSAGE_CREATE" or "GROUP_MESSAGE_CREATE";
}

public class OfficialBotAuthor
{
    [JsonPropertyName("user_openid")]
    public string? UserOpenId { get; set; }
    [JsonPropertyName("member_openid")]
    public string? MemberOpenId { get; set; }
    [JsonPropertyName("bot")]
    public bool Bot { get; set; }
}

public class OfficialBotMessageScene
{
    [JsonPropertyName("ext")]
    public List<string>? Ext { get; set; }
}

public class OfficialBotAttachment
{
    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
