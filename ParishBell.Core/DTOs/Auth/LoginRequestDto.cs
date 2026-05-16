using System.ComponentModel.DataAnnotations;
using ParishBell.Core.Enums;

namespace ParishBell.Core.DTOs.Auth;

public class LoginRequestDto
{
    // NOTE: Provider numeric value - 1=Email, 2=Google, 3=Apple
    [Required(ErrorMessage = "PB-28")]
    public AuthProvider Provider { get; set; }

    // NOTE: Email - required only for Email provider
    [EmailAddress(ErrorMessage = "PB-20")]
    [StringLength(255, ErrorMessage = "PB-21")]
    public string? Email { get; set; }

    // NOTE: Password - required only for Email provider
    public string? Password { get; set; }

    // NOTE: Google ID token - required for Google provider
    public string? IdToken { get; set; }
}
