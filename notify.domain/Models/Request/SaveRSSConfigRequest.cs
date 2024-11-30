using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notify.Domain.Models.Request;

public class SaveRSSConfigRequest
{
    [Required]
    [JsonPropertyName("configs")]
    public required List<RSSConfigDO> Configs { get; set; }
}