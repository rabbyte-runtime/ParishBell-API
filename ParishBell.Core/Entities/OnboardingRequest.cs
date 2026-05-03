using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Parish self-registration requests reviewed by Super Admin.
/// </summary>
[Table("onboarding_requests")]
[Index("LocationId", Name = "idx_or_location")]
[Index("Status", Name = "idx_or_status")]
[Index("SubmittedAt", Name = "idx_or_submitted", AllDescending = true)]
public partial class OnboardingRequest
{
    [Key]
    [Column("request_id")]
    public Guid RequestId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("admin_full_name")]
    [StringLength(255)]
    public string AdminFullName { get; set; } = null!;

    [Column("admin_email")]
    [StringLength(255)]
    public string AdminEmail { get; set; } = null!;

    [Column("admin_phone")]
    [StringLength(30)]
    public string? AdminPhone { get; set; }

    /// <summary>
    /// 1=Pending, 2=Approved, 3=Rejected. Rejected requests never shown again.
    /// </summary>
    [Column("status")]
    public short Status { get; set; }

    /// <summary>
    /// Required when status=3. Emailed to requesting admin.
    /// </summary>
    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("reviewed_by")]
    public Guid? ReviewedBy { get; set; }

    [ForeignKey("LocationId")]
    [InverseProperty("OnboardingRequests")]
    public virtual Location Location { get; set; } = null!;

    [ForeignKey("ReviewedBy")]
    [InverseProperty("OnboardingRequests")]
    public virtual AdminUser? ReviewedByNavigation { get; set; }
}
