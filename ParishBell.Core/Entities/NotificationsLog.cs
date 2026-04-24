using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Log of all push notifications sent. Used for debugging and retry logic.
/// </summary>
[Table("notifications_log")]
[Index("ReferenceId", Name = "idx_nl_reference")]
[Index("IsSent", "SentAt", Name = "idx_nl_sent")]
[Index("Type", Name = "idx_nl_type")]
[Index("UserId", Name = "idx_nl_user")]
public partial class NotificationsLog
{
    [Key]
    [Column("notification_id")]
    public Guid NotificationId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("title")]
    [StringLength(255)]
    public string Title { get; set; } = null!;

    [Column("body")]
    public string Body { get; set; } = null!;

    /// <summary>
    /// 1=Event, 2=Announcement, 3=MassReminder, 4=FeastDay, 5=System.
    /// </summary>
    [Column("type")]
    public short Type { get; set; }

    /// <summary>
    /// ID of related entity — event_id, announcement_id, calendar_id, etc.
    /// </summary>
    [Column("reference_id")]
    public Guid? ReferenceId { get; set; }

    /// <summary>
    /// FALSE if push delivery failed. Retry logic queries is_sent=FALSE.
    /// </summary>
    [Column("is_sent")]
    public bool IsSent { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("NotificationsLogs")]
    public virtual AppUser User { get; set; } = null!;
}
