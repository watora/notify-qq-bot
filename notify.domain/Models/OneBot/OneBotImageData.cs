using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class OneBotImageData
{
    [JsonPropertyName("file")]
    public required string File { get; set; }

    [JsonPropertyName("base64")]
    public required string Base64 { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }

}