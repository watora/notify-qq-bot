using System.Text.Json.Serialization;

namespace Notify.Domain.Models.Response;

public class GetRSSConfigResponse
{
    [JsonPropertyName("rss_config")]
    public required List<RSSConfigDO> RSSConfigs { get; set; }

    [JsonPropertyName("has_next")]
    public required bool HasNext { get; set; }

    [JsonPropertyName("total_count")]
    public long TotalCount { get; set; }
}