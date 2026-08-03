using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.User;

public class RemoveDeviceTokenRequestDto
{
    // NOTE: The token to unregister. Scoped to the caller, so an unknown one is a no-op.
    [Required(ErrorMessage = "PB-58")]
    public string Token { get; set; } = string.Empty;
}
