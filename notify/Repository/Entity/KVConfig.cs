using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Notify.Repository.Entity;

[Table("kv_config")]
public class KVConfig
{
    [Key]
    [Column("key")]
    public required string Key { get; set; }

    [Column("value")]
    public required string Value { get; set; }

    [Column("creator")]
    public required string Creator { get; set; }

    [Column("comment")]
    public required string Comment { get; set; }
}