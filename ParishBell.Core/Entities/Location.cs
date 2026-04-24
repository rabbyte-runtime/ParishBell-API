using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// All Catholic places of worship registered in ParishBell.
/// </summary>
[Table("locations")]
[Index("IsApproved", "IsActive", Name = "idx_locations_approved")]
[Index("Latitude", "Longitude", Name = "idx_locations_coords")]
[Index("IsRejected", Name = "idx_locations_rejected")]
[Index("LocationTypeId", Name = "idx_locations_type")]
public partial class Location
{
    [Key]
    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("location_type_id")]
    public Guid LocationTypeId { get; set; }

    [Column("latitude")]
    [Precision(10, 8)]
    public decimal Latitude { get; set; }

    [Column("longitude")]
    [Precision(11, 8)]
    public decimal Longitude { get; set; }

    [Column("email")]
    [StringLength(255)]
    public string? Email { get; set; }

    [Column("phone")]
    [StringLength(30)]
    public string? Phone { get; set; }

    [Column("website")]
    [StringLength(500)]
    public string? Website { get; set; }

    /// <summary>
    /// Set TRUE by Super Admin. Location visible in app only when TRUE.
    /// </summary>
    [Column("is_approved")]
    public bool IsApproved { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Set TRUE on rejection. Location never shown in app.
    /// </summary>
    [Column("is_rejected")]
    public bool IsRejected { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("approved_by")]
    public Guid? ApprovedBy { get; set; }

    [Column("rejected_at")]
    public DateTime? RejectedAt { get; set; }

    [Column("rejected_by")]
    public Guid? RejectedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Location")]
    public virtual ICollection<AdminUser> AdminUsers { get; set; } = new List<AdminUser>();

    [InverseProperty("Location")]
    public virtual ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();

    [ForeignKey("ApprovedBy")]
    [InverseProperty("LocationApprovedByNavigations")]
    public virtual AdminUser? ApprovedByNavigation { get; set; }

    [InverseProperty("Location")]
    public virtual ICollection<Event> Events { get; set; } = new List<Event>();

    [InverseProperty("Location")]
    public virtual ICollection<LocationFeastDay> LocationFeastDays { get; set; } = new List<LocationFeastDay>();

    [InverseProperty("Location")]
    public virtual ICollection<LocationImage> LocationImages { get; set; } = new List<LocationImage>();

    [InverseProperty("Location")]
    public virtual ICollection<LocationTranslation> LocationTranslations { get; set; } = new List<LocationTranslation>();

    [ForeignKey("LocationTypeId")]
    [InverseProperty("Locations")]
    public virtual LocationType LocationType { get; set; } = null!;

    [InverseProperty("Location")]
    public virtual ICollection<MassSchedule> MassSchedules { get; set; } = new List<MassSchedule>();

    [InverseProperty("Location")]
    public virtual ICollection<OnboardingRequest> OnboardingRequests { get; set; } = new List<OnboardingRequest>();

    [ForeignKey("RejectedBy")]
    [InverseProperty("LocationRejectedByNavigations")]
    public virtual AdminUser? RejectedByNavigation { get; set; }

    [InverseProperty("Location")]
    public virtual ICollection<UserFollowedLocation> UserFollowedLocations { get; set; } = new List<UserFollowedLocation>();
}
