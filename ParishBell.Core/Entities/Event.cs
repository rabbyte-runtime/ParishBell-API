using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Parish events created and published by admins.
/// </summary>
[Table("events")]
[Index("CreatedBy", Name = "idx_ev_created_by")]
[Index("EventDate", Name = "idx_ev_date", AllDescending = true)]
[Index("LocationId", Name = "idx_ev_location")]
[Index("LocationId", "IsPublished", "IsActive", "EventDate", Name = "idx_ev_published", IsDescending = new[] { false, false, false, true })]
public partial class Event
{
    [Key]
    [Column("event_id")]
    public Guid EventId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("event_date")]
    public DateOnly EventDate { get; set; }

    [Column("start_time")]
    public TimeOnly? StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// FALSE=draft. TRUE=visible to users. Publishing triggers push notifications.
    /// </summary>
    [Column("is_published")]
    public bool IsPublished { get; set; }

    /// <summary>
    /// Soft delete. FALSE hides the event without deleting data.
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("Events")]
    public virtual AdminUser CreatedByNavigation { get; set; } = null!;

    [InverseProperty("Event")]
    public virtual ICollection<EventImage> EventImages { get; set; } = new List<EventImage>();

    [InverseProperty("Event")]
    public virtual ICollection<EventTranslation> EventTranslations { get; set; } = new List<EventTranslation>();

    [ForeignKey("LocationId")]
    [InverseProperty("Events")]
    public virtual Location Location { get; set; } = null!;
}
