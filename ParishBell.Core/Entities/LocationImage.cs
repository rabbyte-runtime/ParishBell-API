using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Profile and gallery images. Stored in Azure Blob Storage.
/// </summary>
[Table("location_images")]
[Index("LocationId", Name = "idx_li_location")]
[Index("LocationId", "IsPrimary", Name = "idx_li_primary")]
[Index("LocationId", "SortOrder", Name = "idx_li_sort")]
public partial class LocationImage
{
    [Key]
    [Column("image_id")]
    public Guid ImageId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    /// <summary>
    /// Azure Blob Storage public URL. Compressed JPEG, max 1920x1080px.
    /// </summary>
    [Column("image_url")]
    public string ImageUrl { get; set; } = null!;

    /// <summary>
    /// Only one image per location should be TRUE.
    /// </summary>
    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }

    [Column("uploaded_by")]
    public Guid? UploadedBy { get; set; }

    [ForeignKey("LocationId")]
    [InverseProperty("LocationImages")]
    public virtual Location Location { get; set; } = null!;

    [ForeignKey("UploadedBy")]
    [InverseProperty("LocationImages")]
    public virtual AdminUser? UploadedByNavigation { get; set; }
}
