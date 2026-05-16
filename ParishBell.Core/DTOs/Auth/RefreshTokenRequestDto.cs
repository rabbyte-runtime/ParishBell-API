using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.Auth;

public class RefreshTokenRequestDto
{
    // NOTE: The raw refresh token issued during login or last refresh
    [Required(ErrorMessage = "PB-10")]
    public string RefreshToken { get; set; } = string.Empty;
}
