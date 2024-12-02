using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class OneBotEventMessage: OneBotEventBase
{
    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("message_type")]
    public required string MessageType { get; set; }

    /// <summary>
    /// 消息子类型
    /// </summary>
    [JsonPropertyName("sub_type")]
    public string? SubType { get; set; }

    /// <summary>
    /// 消息id
    /// </summary>
    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    /// <summary>
    /// 群号
    /// </summary>
    [JsonPropertyName("group_id")]
    public long GroupId { get; set; }

    /// <summary>
    /// 发送者qq号
    /// </summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    [JsonPropertyName("message")]
    public required List<OneBotMessageItem> Message { get; set; }

    /// <summary>
    /// 发送者
    /// </summary>
    [JsonPropertyName("sender")]
    public required OneBotEventMessageSender Sender { get; set; }
}

public class OneBotEventMessageSender
{
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }

    [JsonPropertyName("nickname")]
    public required string NickName { get; set; }
}