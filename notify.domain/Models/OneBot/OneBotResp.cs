using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class OneBotResp<T>
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("retcode")]
    public int RetCode { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}