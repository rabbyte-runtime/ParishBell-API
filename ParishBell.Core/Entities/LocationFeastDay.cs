using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Parish-pinned feast days - e.g. patron saint day with procession.
/// </summary>
[Table("location_feast_days")]
[Index("CalendarId", Name = "idx_lfd_calendar")]
[Index("LocationId", Name = "idx_lfd_location")]
[Index("LocationId", "CalendarId", Name = "uq_location_calendar", IsUnique = true)]
public partial class LocationFeastDay
{
    [Key]
    [Column("location_feast_day_id")]
    public Guid LocationFeastDayId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("calendar_id")]
    public Guid CalendarId { get; set; }

    /// <summary>
    /// Shown as a special occasion in the app for this parish.
    /// </summary>
    [Column("is_highlighted")]
    public bool IsHighlighted { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("CalendarId")]
    [InverseProperty("LocationFeastDays")]
    public virtual LiturgicalCalendar Calendar { get; set; } = null!;

    [ForeignKey("LocationId")]
    [InverseProperty("LocationFeastDays")]
    public virtual Location Location { get; set; } = null!;
}
