using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.User;

public class RemoveDeviceTokenRequestDto
{
    // NOTE: The push token to unregister. Removal is scoped to the caller, so an unknown token is a no-op.
    [Required(ErrorMessage = "PB-58")]
    public string Token { get; set; } = string.Empty;
}
