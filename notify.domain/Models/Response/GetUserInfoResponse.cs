using System.Text.Json.Serialization;

namespace Notify.Domain.Models.Response;

public class GetUserInfoResponse
{
    [JsonPropertyName("is_authed")]
    public bool IsAuthed { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}