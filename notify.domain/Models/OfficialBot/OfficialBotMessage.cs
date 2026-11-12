using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

/// <summary>QQ 官方单聊、群聊发送参数；OpenID 与 OneBot 的 QQ 号不通用。</summary>
public class OfficialBotMessage
{
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    [JsonPropertyName("msg_type")]
    public int MsgType { get; set; }

    [JsonPropertyName("msg_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MsgId { get; set; }

    [JsonPropertyName("event_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EventId { get; set; }

    [JsonPropertyName("msg_seq")]
    public int MsgSeq { get; set; } = 1;

    [JsonPropertyName("markdown")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Markdown { get; set; }

    [JsonPropertyName("media")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OfficialBotMedia? Media { get; set; }

    [JsonPropertyName("keyboard")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Keyboard { get; set; }

    [JsonPropertyName("is_wakeup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsWakeup { get; set; }
}

public class OfficialBotMedia
{
    [JsonPropertyName("file_info")]
    public required string FileInfo { get; set; }
}
