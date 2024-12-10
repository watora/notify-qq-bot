using System.Text.Json.Serialization;

namespace Notify.Domain.Models.Request;

public class SetConfigStatusRequest
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}