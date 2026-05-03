using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

/// <summary>
/// User joining a church. Composite PK. Drives push notification targeting.
/// </summary>
[PrimaryKey("UserId", "LocationId")]
[Table("user_followed_locations")]
[Index("LocationId", Name = "idx_ufl_location")]
[Index("UserId", Name = "idx_ufl_user")]
public partial class UserFollowedLocation
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Key]
    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("followed_at")]
    public DateTime FollowedAt { get; set; }

    [ForeignKey("LocationId")]
    [InverseProperty("UserFollowedLocations")]
    public virtual Location Location { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserFollowedLocations")]
    public virtual AppUser User { get; set; } = null!;
}
