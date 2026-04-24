using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Weekly recurring and special one-off mass times per location.
/// </summary>
[Table("mass_schedules")]
[Index("LocationId", "IsActive", Name = "idx_ms_active")]
[Index("DayOfWeek", "MassTime", Name = "idx_ms_day_time")]
[Index("LocationId", Name = "idx_ms_location")]
public partial class MassSchedule
{
    [Key]
    [Column("schedule_id")]
    public Guid ScheduleId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    /// <summary>
    /// 0=Sunday, 1=Monday, 2=Tuesday, 3=Wednesday, 4=Thursday, 5=Friday, 6=Saturday.
    /// </summary>
    [Column("day_of_week")]
    public int DayOfWeek { get; set; }

    [Column("mass_time")]
    public TimeOnly MassTime { get; set; }

    /// <summary>
    /// TRUE for seasonal/one-off masses. Requires valid_from and valid_to.
    /// </summary>
    [Column("is_special")]
    public bool IsSpecial { get; set; }

    [Column("valid_from")]
    public DateOnly? ValidFrom { get; set; }

    [Column("valid_to")]
    public DateOnly? ValidTo { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("LocationId")]
    [InverseProperty("MassSchedules")]
    public virtual Location Location { get; set; } = null!;

    [InverseProperty("Schedule")]
    public virtual ICollection<MassScheduleTranslation> MassScheduleTranslations { get; set; } = new List<MassScheduleTranslation>();

    [InverseProperty("Schedule")]
    public virtual ICollection<UserMassReminder> UserMassReminders { get; set; } = new List<UserMassReminder>();
}
