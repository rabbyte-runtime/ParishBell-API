using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Parish admins and Super Admin. Separate from app users.
/// </summary>
[Table("admin_users")]
[Index("IsActive", Name = "idx_admin_active")]
[Index("LocationId", Name = "idx_admin_location")]
[Index("Role", Name = "idx_admin_role")]
[Index("Email", Name = "uq_admin_email", IsUnique = true)]
public partial class AdminUser
{
    [Key]
    [Column("admin_id")]
    public Guid AdminId { get; set; }

    [Column("location_id")]
    public Guid? LocationId { get; set; }

    [Column("full_name")]
    [StringLength(255)]
    public string FullName { get; set; } = null!;

    [Column("email")]
    [StringLength(255)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// BCrypt hashed. Raw password never stored.
    /// </summary>
    [Column("password_hash")]
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// 1=SuperAdmin, 2=Admin. SuperAdmin has NULL location_id.
    /// </summary>
    [Column("role")]
    public short Role { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();

    [InverseProperty("UploadedByNavigation")]
    public virtual ICollection<EventImage> EventImages { get; set; } = new List<EventImage>();

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<Event> Events { get; set; } = new List<Event>();

    [ForeignKey("LocationId")]
    [InverseProperty("AdminUsers")]
    public virtual Location? Location { get; set; }

    [InverseProperty("ApprovedByNavigation")]
    public virtual ICollection<Location> LocationApprovedByNavigations { get; set; } = new List<Location>();

    [InverseProperty("UploadedByNavigation")]
    public virtual ICollection<LocationImage> LocationImages { get; set; } = new List<LocationImage>();

    [InverseProperty("RejectedByNavigation")]
    public virtual ICollection<Location> LocationRejectedByNavigations { get; set; } = new List<Location>();

    [InverseProperty("ReviewedByNavigation")]
    public virtual ICollection<OnboardingRequest> OnboardingRequests { get; set; } = new List<OnboardingRequest>();
}
