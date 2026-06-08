using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.Auth;

public class ResetPasswordRequestDto
{
    // NOTE: Email of the account being reset
    [Required(ErrorMessage = "PB-19")]
    [EmailAddress(ErrorMessage = "PB-20")]
    [StringLength(255, ErrorMessage = "PB-21")]
    public string Email { get; set; } = string.Empty;

    // NOTE: 6-digit code received in the reset email
    [Required(ErrorMessage = "PB-35")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "PB-35")]
    public string Code { get; set; } = string.Empty;

    // NOTE: New password
    [Required(ErrorMessage = "PB-22")]
    public string NewPassword { get; set; } = string.Empty;

    // NOTE: Confirmation
    [Required(ErrorMessage = "PB-23")]
    public string ConfirmPassword { get; set; } = string.Empty;
}