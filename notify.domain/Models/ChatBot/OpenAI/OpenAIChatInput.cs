using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class OpenAIChatInput
{
    [JsonPropertyName("model")]
    public required string Model { get; set; }

    [JsonPropertyName("messages")]
    public required List<OpenAIChatInputMessage> Messages { get; set; }
}

public class OpenAIChatInputMessage
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public required List<OpenAIChatInputMessageContent> Content { get; set; }
}

public class OpenAIChatInputMessageContent
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIChatInputContentImage? ImageUrl { get; set; }
}

public class OpenAIChatInputContentImage
{
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}