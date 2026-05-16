using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParishBell.Core.Entities;

[Table("password_reset_tokens")]
public partial class PasswordResetToken
{
    [Key]
    [Column("reset_token_id")]
    public Guid ResetTokenId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("code_hash")]
    [StringLength(255)]
    public string CodeHash { get; set; } = null!;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("is_used")]
    public bool IsUsed { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by_ip")]
    [StringLength(45)]
    public string? CreatedByIp { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("PasswordResetTokens")]
    public virtual AppUser User { get; set; } = null!;
}
