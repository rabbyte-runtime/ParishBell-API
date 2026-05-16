using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

[Table("messages")]
[Index("MessageCode", Name = "idx_messages_key", IsUnique = true)]
[Index("MessageCode", Name = "messages_message_code_key", IsUnique = true)]
public partial class Message
{
    [Key]
    [Column("message_id")]
    public Guid MessageId { get; set; }

    [Column("message_code")]
    public string MessageCode { get; set; } = null!;

    [Column("message_type")]
    public string MessageType { get; set; } = null!;

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [InverseProperty("MessageNavigation")]
    public virtual ICollection<MessageTranslation> MessageTranslations { get; set; } = new List<MessageTranslation>();
}
