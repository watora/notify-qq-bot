
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class EntityConfigDO 
{   
    [Required]
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [Required]
    [JsonPropertyName("value")]
    public string? Value { get; set; }
    
    [JsonPropertyName("creator")]
    public string? Creator { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

}