using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class OpenAIResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    // Preserve complete items (including reasoning/phase) for stateless conversation replay.
    [JsonPropertyName("output")]
    public List<JsonElement>? Output { get; set; }
    [JsonPropertyName("error")]
    public JsonElement? Error { get; set; }
    [JsonPropertyName("incomplete_details")]
    public JsonElement? IncompleteDetails { get; set; }
    [JsonPropertyName("usage")]
    public JsonElement? Usage { get; set; }

    [JsonIgnore]
    public string OutputText => string.Concat(GetContent("output_text", "text"));

    public string GetReplyText()
    {
        var text = OutputText;
        return string.IsNullOrWhiteSpace(text) ? string.Join("\n", GetContent("refusal", "refusal")) : text;
    }

    private IEnumerable<string> GetContent(string type, string field)
    {
        foreach (var item in Output ?? Enumerable.Empty<JsonElement>())
        {
            if (GetString(item, "type") != "message" || GetString(item, "role") != "assistant"
                || !item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var part in content.EnumerateArray())
                if (GetString(part, "type") == type && GetString(part, field) is { } value)
                    yield return value;
        }
    }

    private static string? GetString(JsonElement item, string field) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}
