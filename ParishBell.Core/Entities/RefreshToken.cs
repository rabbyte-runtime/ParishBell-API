using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

[Table("refresh_tokens")]
[Index("ExpiresAt", Name = "idx_rt_expires")]
[Index("IsRevoked", Name = "idx_rt_revoked")]
[Index("TokenHash", Name = "idx_rt_token_hash")]
[Index("UserId", Name = "idx_rt_user")]
[Index("TokenHash", Name = "uq_rt_token_hash", IsUnique = true)]
public partial class RefreshToken
{
    [Key]
    [Column("refresh_token_id")]
    public Guid RefreshTokenId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("token_hash")]
    public string TokenHash { get; set; } = null!;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by_ip")]
    [StringLength(45)]
    public string? CreatedByIp { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [Column("is_revoked")]
    public bool IsRevoked { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("RefreshTokens")]
    public virtual AppUser User { get; set; } = null!;
}
