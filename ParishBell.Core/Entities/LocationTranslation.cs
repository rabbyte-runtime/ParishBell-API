using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Multilingual names, descriptions, and addresses for each location.
/// </summary>
[Table("location_translations")]
[Index("LanguageId", Name = "idx_lt_language")]
[Index("LocationId", Name = "idx_lt_location")]
[Index("LocationId", "LanguageId", Name = "uq_location_lang", IsUnique = true)]
public partial class LocationTranslation
{
    [Key]
    [Column("translation_id")]
    public Guid TranslationId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("language_id")]
    public Guid LanguageId { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [ForeignKey("LanguageId")]
    [InverseProperty("LocationTranslations")]
    public virtual Language Language { get; set; } = null!;

    [ForeignKey("LocationId")]
    [InverseProperty("LocationTranslations")]
    public virtual Location Location { get; set; } = null!;
}
