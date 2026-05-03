using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Multilingual titles and descriptions for each liturgical entry.
/// </summary>
[Table("liturgical_calendar_translations")]
[Index("CalendarId", Name = "idx_lct_calendar")]
[Index("CalendarId", "LanguageId", Name = "uq_calendar_lang", IsUnique = true)]
public partial class LiturgicalCalendarTranslation
{
    [Key]
    [Column("translation_id")]
    public Guid TranslationId { get; set; }

    [Column("calendar_id")]
    public Guid CalendarId { get; set; }

    [Column("language_id")]
    public Guid LanguageId { get; set; }

    [Column("title")]
    [StringLength(255)]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [ForeignKey("CalendarId")]
    [InverseProperty("LiturgicalCalendarTranslations")]
    public virtual LiturgicalCalendar Calendar { get; set; } = null!;

    [ForeignKey("LanguageId")]
    [InverseProperty("LiturgicalCalendarTranslations")]
    public virtual Language Language { get; set; } = null!;
}
