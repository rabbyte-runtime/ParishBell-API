using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// Push notification tokens per user. One user may have multiple devices.
/// </summary>
[Table("user_devices")]
[Index("LastActiveAt", Name = "idx_ud_last_active")]
[Index("Platform", Name = "idx_ud_platform")]
[Index("UserId", Name = "idx_ud_user")]
[Index("DeviceToken", Name = "uq_device_token", IsUnique = true)]
public partial class UserDevice
{
    [Key]
    [Column("device_id")]
    public Guid DeviceId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("device_token")]
    public string DeviceToken { get; set; } = null!;

    /// <summary>
    /// 1=iOS (APNs), 2=Android (FCM).
    /// </summary>
    [Column("platform")]
    public short Platform { get; set; }

    [Column("registered_at")]
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Tokens inactive &gt; 90 days are pruned by background job.
    /// </summary>
    [Column("last_active_at")]
    public DateTime LastActiveAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserDevices")]
    public virtual AppUser User { get; set; } = null!;
}
