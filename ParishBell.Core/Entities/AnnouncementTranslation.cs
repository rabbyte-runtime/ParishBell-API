using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Optional multilingual captions for announcements.
/// </summary>
[Table("announcement_translations")]
[Index("AnnouncementId", Name = "idx_at_announcement")]
[Index("AnnouncementId", "LanguageId", Name = "uq_announcement_lang", IsUnique = true)]
public partial class AnnouncementTranslation
{
    [Key]
    [Column("translation_id")]
    public Guid TranslationId { get; set; }

    [Column("announcement_id")]
    public Guid AnnouncementId { get; set; }

    [Column("language_id")]
    public Guid LanguageId { get; set; }

    [Column("caption")]
    [StringLength(500)]
    public string? Caption { get; set; }

    /// <summary>
    /// Optional per-language headline shown in the channel list.
    /// </summary>
    [Column("title")]
    [StringLength(255)]
    public string? Title { get; set; }

    /// <summary>
    /// Optional per-language long-form body.
    /// </summary>
    [Column("description")]
    public string? Description { get; set; }

    [ForeignKey("AnnouncementId")]
    [InverseProperty("AnnouncementTranslations")]
    public virtual Announcement Announcement { get; set; } = null!;

    [ForeignKey("LanguageId")]
    [InverseProperty("AnnouncementTranslations")]
    public virtual Language Language { get; set; } = null!;
}
