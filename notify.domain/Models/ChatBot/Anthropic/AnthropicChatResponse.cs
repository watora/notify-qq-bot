using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class AnthropicChatResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("content")]
    public required List<AnthropicChatResponseContent> Content { get; set; }

    [JsonPropertyName("model")]
    public required string Model { get; set; }

    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("stop_reason")]
    public required string StopReason { get; set; }

    [JsonPropertyName("usage")]
    public required AnthropicChatResponseUsage Usage { get; set; }

}

public class AnthropicChatResponseContent
{
    /// <summary>
    /// 只处理text
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

}

public class AnthropicChatResponseUsage
{
    [JsonPropertyName("input_tokens")]
    public required int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public required int OutputTokens { get; set; }
}
