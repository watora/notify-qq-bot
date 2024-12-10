using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notify.Domain.Models.Request;

public class DelEntityConfigRequest
{
    [Required]
    [JsonPropertyName("key")]
    public required string Key { get; set; }
}