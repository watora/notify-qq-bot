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

public enum OneBotMessageDataKey
{
    /// <summary>
    /// 纯文本
    /// </summary>
    [EnumMember(Value = "text")]
    Text,
    /// <summary>
    /// 表情/回复的id
    /// </summary>
    [EnumMember(Value = "id")]
    Id,
    /// <summary>
    /// 发送时是url 接收时是文件名
    /// </summary>
    [EnumMember(Value = "file")]
    File,
    /// <summary>
    /// qq号
    /// </summary>
    [EnumMember(Value = "qq")]
    QQ,
    /// <summary>
    /// 文件的url
    /// </summary>
    [EnumMember(Value = "url")]
    Url,
    /// <summary>
    /// 分享的标题
    /// </summary>
    [EnumMember(Value = "title")]
    Title,
}

public class OneBotMessage
{
    public List<OneBotMessageItem> Items { get; init; } = new List<OneBotMessageItem>();

    /// <summary>
    /// 序列化为字符串格式
    /// </summary>
    /// <returns></returns>
    public string SerializeToString()
    {
        var builder = new StringBuilder();
        foreach (var msg in this.Items)
        {
            if (msg.Type == OneBotMessageType.Text.ToCustomString())
            {
                builder.Append(encode(msg.Data[OneBotMessageDataKey.Text.ToCustomString()], true));
            }
            else
            {
                builder.Append('[');
                builder.Append($"CQ:{msg.Type}");
                foreach (var kv in msg.Data)
                {
                    builder.Append($",{kv.Key}={encode(kv.Value, false)}");
                }
                builder.Append(']');
            }
        }
        return builder.ToString();
    }

    private string encode(string data, bool isText)
    {
        var encoded = data.Replace("[", "&#91;").Replace("]", "&#93;").Replace("&", "&amp;");
        if (!isText)
        {
            encoded = encoded.Replace(",", "&#44;");
        }
        return encoded;
    }
}

public class OneBotMessageItem
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    [JsonPropertyName("data")]
    public required Dictionary<string, string> Data { get; set; }
}