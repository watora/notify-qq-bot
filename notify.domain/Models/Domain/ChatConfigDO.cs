
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class ChatConfigDO 
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [Required]
    [JsonPropertyName("target_id")]
    public string? TargetId { get; set; }

    [Required]
    [JsonPropertyName("target_type")]
    public string? TargetType { get; set; }

    [Required]
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [Required]
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [Required]
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
    
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("creator")]
    public string? Creator { get; set; }
}