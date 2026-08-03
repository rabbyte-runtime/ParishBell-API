using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.Auth;

public class LogoutRequestDto
{
    // NOTE: The refresh token to revoke (current device's session)
    [Required(ErrorMessage = "PB-10")]
    public string RefreshToken { get; set; } = string.Empty;

    // NOTE: Optional — the calling device's push token. When supplied, its registration is pruned so
    // NOTE: The signed-out device then stops receiving notifications.
    public string? DeviceToken { get; set; }
}
