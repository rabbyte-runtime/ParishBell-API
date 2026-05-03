using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Time-limited audio/video parish channel announcements for joined members only.
/// </summary>
[Table("announcements")]
[Index("IsActive", "ExpiresAt", Name = "idx_ann_active")]
[Index("ExpiresAt", Name = "idx_ann_expires")]
[Index("LocationId", Name = "idx_ann_location")]
public partial class Announcement
{
    [Key]
    [Column("announcement_id")]
    public Guid AnnouncementId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    /// <summary>
    /// Azure Blob Storage SAS URL for the audio/video file.
    /// </summary>
    [Column("media_url")]
    public string MediaUrl { get; set; } = null!;

    /// <summary>
    /// 1=Audio, 2=Video.
    /// </summary>
    [Column("media_type")]
    public short MediaType { get; set; }

    [Column("duration_seconds")]
    public int DurationSeconds { get; set; }

    /// <summary>
    /// Between created_at+1hr and created_at+7days. Background job sets is_active=FALSE on expiry.
    /// </summary>
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [InverseProperty("Announcement")]
    public virtual ICollection<AnnouncementTranslation> AnnouncementTranslations { get; set; } = new List<AnnouncementTranslation>();

    [ForeignKey("CreatedBy")]
    [InverseProperty("Announcements")]
    public virtual AdminUser CreatedByNavigation { get; set; } = null!;

    [ForeignKey("LocationId")]
    [InverseProperty("Announcements")]
    public virtual Location Location { get; set; } = null!;
}
