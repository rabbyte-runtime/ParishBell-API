using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Multilingual titles and descriptions for each event.
/// </summary>
[Table("event_translations")]
[Index("EventId", Name = "idx_et_event")]
[Index("LanguageId", Name = "idx_et_language")]
[Index("EventId", "LanguageId", Name = "uq_event_lang", IsUnique = true)]
public partial class EventTranslation
{
    [Key]
    [Column("translation_id")]
    public Guid TranslationId { get; set; }

    [Column("event_id")]
    public Guid EventId { get; set; }

    [Column("language_id")]
    public Guid LanguageId { get; set; }

    [Column("title")]
    [StringLength(255)]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [ForeignKey("EventId")]
    [InverseProperty("EventTranslations")]
    public virtual Event Event { get; set; } = null!;

    [ForeignKey("LanguageId")]
    [InverseProperty("EventTranslations")]
    public virtual Language Language { get; set; } = null!;
}
