using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Registered ParishBell mobile app users.
/// </summary>
[Table("app_users")]
[Index("IsActive", Name = "idx_app_users_active")]
[Index("PreferredLanguage", Name = "idx_app_users_language")]
[Index("AuthProvider", "AuthProviderId", Name = "idx_app_users_provider")]
[Index("Email", Name = "uq_app_users_email", IsUnique = true)]
public partial class AppUser
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("full_name")]
    [StringLength(255)]
    public string FullName { get; set; } = null!;

    [Column("email")]
    [StringLength(255)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// BCrypt hashed. NULL for social auth users.
    /// </summary>
    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    /// <summary>
    /// 1=Email, 2=Google, 3=Apple.
    /// </summary>
    [Column("auth_provider")]
    public short AuthProvider { get; set; }

    /// <summary>
    /// Google/Apple subject ID. NULL for email auth users.
    /// </summary>
    [Column("auth_provider_id")]
    public string? AuthProviderId { get; set; }

    [Column("profile_image_url")]
    public string? ProfileImageUrl { get; set; }

    [Column("preferred_language")]
    public Guid PreferredLanguage { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<NotificationsLog> NotificationsLogs { get; set; } = new List<NotificationsLog>();

    [ForeignKey("PreferredLanguage")]
    [InverseProperty("AppUsers")]
    public virtual Language PreferredLanguageNavigation { get; set; } = null!;

    [InverseProperty("User")]
    public virtual ICollection<UserDevice> UserDevices { get; set; } = new List<UserDevice>();

    [InverseProperty("User")]
    public virtual ICollection<UserFollowedLocation> UserFollowedLocations { get; set; } = new List<UserFollowedLocation>();

    [InverseProperty("User")]
    public virtual ICollection<UserMassReminder> UserMassReminders { get; set; } = new List<UserMassReminder>();
}
