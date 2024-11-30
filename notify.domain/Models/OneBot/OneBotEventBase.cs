using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class OneBotEventBase {
    [JsonPropertyName("time")]
    public long Time { get; set; }

    /// <summary>
    /// QQ号
    /// </summary>
    [JsonPropertyName("self_id")]
    public long SelfId { get; set; }

    /// <summary>
    /// 事件类型
    /// </summary>
    [JsonPropertyName("post_type")]
    public required string PostType { get; set; }
}