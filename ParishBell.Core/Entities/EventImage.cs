using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Photos for past events only. API enforces event_date must be in the past.
/// </summary>
[Table("event_images")]
[Index("EventId", "SortOrder", Name = "idx_ei_event")]
public partial class EventImage
{
    [Key]
    [Column("event_image_id")]
    public Guid EventImageId { get; set; }

    [Column("event_id")]
    public Guid EventId { get; set; }

    /// <summary>
    /// Azure Blob Storage URL. Compressed JPEG, max 1200x1200px.
    /// </summary>
    [Column("image_url")]
    public string ImageUrl { get; set; } = null!;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }

    [Column("uploaded_by")]
    public Guid UploadedBy { get; set; }

    [ForeignKey("EventId")]
    [InverseProperty("EventImages")]
    public virtual Event Event { get; set; } = null!;

    [ForeignKey("UploadedBy")]
    [InverseProperty("EventImages")]
    public virtual AdminUser UploadedByNavigation { get; set; } = null!;
}
