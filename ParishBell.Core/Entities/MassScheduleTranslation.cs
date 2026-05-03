using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Multilingual labels for mass schedule entries.
/// </summary>
[Table("mass_schedule_translations")]
[Index("ScheduleId", Name = "idx_mst_schedule")]
[Index("ScheduleId", "LanguageId", Name = "uq_schedule_lang", IsUnique = true)]
public partial class MassScheduleTranslation
{
    [Key]
    [Column("translation_id")]
    public Guid TranslationId { get; set; }

    [Column("schedule_id")]
    public Guid ScheduleId { get; set; }

    [Column("language_id")]
    public Guid LanguageId { get; set; }

    /// <summary>
    /// e.g. Sunday Family Mass, Sinhala Mass, Confession.
    /// </summary>
    [Column("label")]
    [StringLength(255)]
    public string Label { get; set; } = null!;

    [ForeignKey("LanguageId")]
    [InverseProperty("MassScheduleTranslations")]
    public virtual Language Language { get; set; } = null!;

    [ForeignKey("ScheduleId")]
    [InverseProperty("MassScheduleTranslations")]
    public virtual MassSchedule Schedule { get; set; } = null!;
}
