using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class OfficialBotSendRequest
{
    [JsonPropertyName("target_id")]
    public string? TargetId { get; set; }

    [JsonPropertyName("message_type")]
    public string? MessageType { get; set; }

    [JsonPropertyName("message")]
    public OfficialBotMessage? Message { get; set; }
}
