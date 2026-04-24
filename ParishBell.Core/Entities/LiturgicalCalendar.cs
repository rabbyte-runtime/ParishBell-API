using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Shared Catholic calendar of feast days, Holy Days, seasons, novenas.
/// </summary>
[Table("liturgical_calendar")]
[Index("IsHolyDay", Name = "idx_lc_holyday")]
[Index("IsRecurringAnnually", "Month", "Day", Name = "idx_lc_recurring")]
[Index("SpecificDate", Name = "idx_lc_specific_date")]
public partial class LiturgicalCalendar
{
    [Key]
    [Column("calendar_id")]
    public Guid CalendarId { get; set; }

    [Column("month")]
    public int? Month { get; set; }

    [Column("day")]
    public int? Day { get; set; }

    [Column("specific_date")]
    public DateOnly? SpecificDate { get; set; }

    /// <summary>
    /// TRUE=fixed annual (month+day). FALSE=one-off (specific_date).
    /// </summary>
    [Column("is_recurring_annually")]
    public bool IsRecurringAnnually { get; set; }

    /// <summary>
    /// TRUE for Holy Days of Obligation.
    /// </summary>
    [Column("is_holy_day")]
    public bool IsHolyDay { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Calendar")]
    public virtual ICollection<LiturgicalCalendarTranslation> LiturgicalCalendarTranslations { get; set; } = new List<LiturgicalCalendarTranslation>();

    [InverseProperty("Calendar")]
    public virtual ICollection<LocationFeastDay> LocationFeastDays { get; set; } = new List<LocationFeastDay>();
}
