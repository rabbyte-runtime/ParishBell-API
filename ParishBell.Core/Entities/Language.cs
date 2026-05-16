using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Supported UI and content languages.
/// </summary>
[Table("languages")]
[Index("IsActive", Name = "idx_languages_active")]
[Index("LanguageCode", Name = "idx_languages_code")]
[Index("LanguageCode", Name = "uq_languages_code", IsUnique = true)]
public partial class Language
{
    [Key]
    [Column("language_id")]
    public Guid LanguageId { get; set; }

    /// <summary>
    /// ISO 639-1 code: en, si, ta
    /// </summary>
    [Column("language_code")]
    [StringLength(5)]
    public string LanguageCode { get; set; } = null!;

    [Column("language_name")]
    [StringLength(50)]
    public string LanguageName { get; set; } = null!;

    /// <summary>
    /// Name in the language itself - e.g. සිංහල, தமிழ்
    /// </summary>
    [Column("native_name")]
    [StringLength(50)]
    public string NativeName { get; set; } = null!;

    [Column("is_active")]
    public bool IsActive { get; set; }

    [InverseProperty("Language")]
    public virtual ICollection<AnnouncementTranslation> AnnouncementTranslations { get; set; } = new List<AnnouncementTranslation>();

    [InverseProperty("PreferredLanguageNavigation")]
    public virtual ICollection<AppUser> AppUsers { get; set; } = new List<AppUser>();

    [InverseProperty("Language")]
    public virtual ICollection<EventTranslation> EventTranslations { get; set; } = new List<EventTranslation>();

    [InverseProperty("Language")]
    public virtual ICollection<LiturgicalCalendarTranslation> LiturgicalCalendarTranslations { get; set; } = new List<LiturgicalCalendarTranslation>();

    [InverseProperty("Language")]
    public virtual ICollection<LocationTranslation> LocationTranslations { get; set; } = new List<LocationTranslation>();

    [InverseProperty("Language")]
    public virtual ICollection<LocationTypeTranslation> LocationTypeTranslations { get; set; } = new List<LocationTypeTranslation>();

    [InverseProperty("Language")]
    public virtual ICollection<MassScheduleTranslation> MassScheduleTranslations { get; set; } = new List<MassScheduleTranslation>();

    [InverseProperty("Language")]
    public virtual ICollection<MessageTranslation> MessageTranslations { get; set; } = new List<MessageTranslation>();
}
