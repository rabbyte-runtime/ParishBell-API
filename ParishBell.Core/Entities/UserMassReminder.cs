using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Personal push reminders set by users for specific mass times.
/// </summary>
[Table("user_mass_reminders")]
[Index("IsActive", Name = "idx_umr_active")]
[Index("ScheduleId", Name = "idx_umr_schedule")]
[Index("UserId", Name = "idx_umr_user")]
[Index("UserId", "ScheduleId", Name = "uq_user_schedule", IsUnique = true)]
public partial class UserMassReminder
{
    [Key]
    [Column("reminder_id")]
    public Guid ReminderId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("schedule_id")]
    public Guid ScheduleId { get; set; }

    /// <summary>
    /// e.g. 15, 30, 60. Background job triggers push at (mass_time - minutes_before).
    /// </summary>
    [Column("minutes_before")]
    public int MinutesBefore { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ScheduleId")]
    [InverseProperty("UserMassReminders")]
    public virtual MassSchedule Schedule { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserMassReminders")]
    public virtual AppUser User { get; set; } = null!;
}
