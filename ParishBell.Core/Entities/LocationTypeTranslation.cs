using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Translated names for each location type per language.
/// </summary>
[Table("location_type_translations")]
[Index("LanguageId", Name = "idx_ltt_language")]
[Index("LocationTypeId", Name = "idx_ltt_type")]
[Index("LocationTypeId", "LanguageId", Name = "uq_location_type_lang", IsUnique = true)]
public partial class LocationTypeTranslation
{
    [Key]
    [Column("translation_id")]
    public Guid TranslationId { get; set; }

    [Column("location_type_id")]
    public Guid LocationTypeId { get; set; }

    [Column("language_id")]
    public Guid LanguageId { get; set; }

    [Column("name")]
    [StringLength(100)]
    public string Name { get; set; } = null!;

    [ForeignKey("LanguageId")]
    [InverseProperty("LocationTypeTranslations")]
    public virtual Language Language { get; set; } = null!;

    [ForeignKey("LocationTypeId")]
    [InverseProperty("LocationTypeTranslations")]
    public virtual LocationType LocationType { get; set; } = null!;
}
