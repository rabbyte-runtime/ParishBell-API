using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.Auth;

public class ForgotPasswordRequestDto
{
    // NOTE: Email of the account requesting password reset
    [Required(ErrorMessage = "PB-19")]
    [EmailAddress(ErrorMessage = "PB-20")]
    [StringLength(255, ErrorMessage = "PB-21")]
    public string Email { get; set; } = string.Empty;
}