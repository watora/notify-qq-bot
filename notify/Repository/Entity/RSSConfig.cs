using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Notify.Repository.Entity;

[Table("rss_config")]
public class RSSConfig
{
    [Key]
    [Column("id")]
    public long? Id { get; set; }

    [Column("subscribe_id")]
    public required string SubscribeId { get; set; }

    [Column("subscribe_channel")]
    public required string SubscribeChannel { get; set; }

    [Column("msg_target_id")]
    public required string MsgTargetId { get; set; }

    [Column("msg_target_type")]
    public required string MsgTargetType { get; set; }

    [Column("last_msg_send_time")]
    public long LastMsgSendTime { get; set; }

    [Column("last_check_time")]
    public long LastCheckTime { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("check_interval")]
    public long CheckInterval { get; set; }

    [Column("exp_check_time")]
    public long ExpCheckTime { get; set; }

    [Column("creator")]
    public required string Creator { get; set; }

    [Column("comment")]
    public required string Comment { get; set; }
}
