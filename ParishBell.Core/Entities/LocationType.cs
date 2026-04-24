using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Categories of Catholic locations - Church, Cathedral, Shrine, etc.
/// </summary>
[Table("location_types")]
[Index("SortOrder", Name = "idx_location_types_sort")]
public partial class LocationType
{
    [Key]
    [Column("location_type_id")]
    public Guid LocationTypeId { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("LocationType")]
    public virtual ICollection<LocationTypeTranslation> LocationTypeTranslations { get; set; } = new List<LocationTypeTranslation>();

    [InverseProperty("LocationType")]
    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
}
