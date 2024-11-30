using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notify.Domain.Models;

public class RSSConfigDO
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("subscribe_channel")]
    public required string SubscribeChannel { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("subscribe_id")]
    public required string SubscribeId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("msg_target_id")]
    public required string MsgTargetId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("msg_target_type")]
    public required string MsgTargetType { get; set; }

    [JsonPropertyName("last_check_time")]
    public long LastCheckTime { get; set; }

    [JsonPropertyName("last_check_time_str")]
    public string? LastCheckTimeStr { get; set; }

    [JsonPropertyName("last_msg_send_time_str")]
    public long LastMsgSendTime { get; set; }

    [JsonPropertyName("last_msg_send_time")]
    public string? LastMsgSendTimeStr { get; set; }

    [Range(60, 3600)]
    [JsonPropertyName("check_interval")]
    public long CheckInterval { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("exp_check_time")]
    public long ExpCheckTime { get; set; }

    [JsonPropertyName("exp_check_time_str")]
    public string? ExpCheckTimeStr { get; set; }

    [JsonPropertyName("creator")]
    public required string Creator { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("comment")]
    public required string Comment { get; set; }
}