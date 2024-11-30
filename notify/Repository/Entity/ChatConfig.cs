using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Notify.Repository.Entity;

[Table("chat_config")]
public class ChatConfig
{
    [Key]
    [Column("id")]
    public long? Id { get; set; }

    [Column("target_id")]
    public required string TargetId { get; set; }

    [Column("target_type")]
    public required string TargetType { get; set; }

    [Column("model")]
    public required string Model { get; set; }

    [Column("provider")]
    public required string Provider { get; set; }

    [Column("comment")]
    public required string Comment { get; set; }
    
    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("creator")]
    public required string Creator { get; set; }
}