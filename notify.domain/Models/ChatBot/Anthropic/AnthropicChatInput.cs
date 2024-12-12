using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class AnthropicChatInput
{
    [JsonPropertyName("model")]
    public required string Model { get; set; }

    [JsonPropertyName("messages")]
    public required List<AnthropicChatInputMessage> Messages { get; set; }

    [JsonPropertyName("max_tokens")]
    public required int MaxTokens { get; set; }
}

public class AnthropicChatInputMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public required List<AnthropicChatInputMessageContent> Content { get; set; }
}

public class AnthropicChatInputMessageContent
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnthropicChatInputContentSource? Source { get; set; }
}

public class AnthropicChatInputContentSource
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("media_type")]
    public required string MediaType { get; set; }

    [JsonPropertyName("data")]
    public required string Data { get; set; }
}