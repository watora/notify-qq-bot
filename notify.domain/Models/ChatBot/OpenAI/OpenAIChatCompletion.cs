using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class OpenAIChatCompletion
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("choices")]
    public required List<OpenAIChatCompletionChoice> Choices { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public required string Model { get; set; }

    [JsonPropertyName("usage")]
    public OpenAIChatCompletionUsage? Usage { get; set; }
}

public class OpenAIChatCompletionChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public required OpenAIChatCompletionMessage Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class OpenAIChatCompletionMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public required string Content { get; set; }

}

public class OpenAIChatCompletionUsage
{
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}