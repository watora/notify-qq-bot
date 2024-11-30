using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notify.Domain.Models.Request;

public class SaveChatConfigRequest
{
    [Required]
    [JsonPropertyName("configs")]
    public required List<ChatConfigDO> Configs { get; set; }
}