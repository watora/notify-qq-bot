using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using Notify.Domain.Utils;

namespace Notify.Domain.Models;

public enum OneBotMessageType
{
    /// <summary>
    /// 文本
    /// </summary>
    [EnumMember(Value = "text")]
    Text,
    /// <summary>
    /// 图片
    /// </summary>
    [EnumMember(Value = "image")]
    CQImage,
    /// <summary>
    /// 表情
    /// </summary>
    [EnumMember(Value = "face")]
    CQFace,
    /// <summary>
    /// 分享
    /// </summary>
    [EnumMember(Value = "share")]
    CQShare,
    /// <summary>
    /// @
    /// </summary>
    [EnumMember(Value = "at")]
    CQAt,
    /// <summary>
    /// 回复
    /// </summary>
    [EnumMember(Value = "reply")]
    CQReply,
}

public class OneBotMessage
{
    public List<OneBotMessageItem> Items { get; init; } = new List<OneBotMessageItem>();
}

public class OneBotMessageItem
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("data")]
    public required OneBotMessageItemData Data { get; set; }
}

public class OneBotMessageItemData
{
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }

    [JsonPropertyName("qq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? QQ { get; set; }

}