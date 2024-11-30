using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class GithubUserInfo
{
    [JsonPropertyName("login")]
    public required string Login { get; set; }

    [JsonPropertyName("id")]
    public required long Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("avatar_url")]
    public required string AvatarUrl { get; set; }
}