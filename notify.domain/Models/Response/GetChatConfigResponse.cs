using System.Text.Json.Serialization;

namespace Notify.Domain.Models.Response;

public class GetChatConfigResponse
{
    [JsonPropertyName("chat_config")]
    public required List<ChatConfigDO> ChatConfigs { get; set; }

    [JsonPropertyName("has_next")]
    public required bool HasNext { get; set; }

    [JsonPropertyName("total_count")]
    public long TotalCount { get; set; }
}