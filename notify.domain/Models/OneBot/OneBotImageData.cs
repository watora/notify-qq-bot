using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class OneBotImageData
{
    [JsonPropertyName("file")]
    public required string File { get; set; }

    [JsonPropertyName("base64")]
    public string? Base64 { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }

    [JsonIgnore]
    public string? MimeType { get; set; }

}