using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notify.Domain.Models.Request;

public class SaveConfigsRequest<T>
{
    [Required]
    [JsonPropertyName("configs")]
    public required List<T> Configs { get; set; }
}