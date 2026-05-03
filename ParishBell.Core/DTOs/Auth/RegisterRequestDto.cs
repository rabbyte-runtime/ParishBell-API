using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.Auth;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "PB-17")]
    [StringLength(255, MinimumLength = 4, ErrorMessage = "PB-18")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "PB-19")]
    [EmailAddress(ErrorMessage = "PB-20")]
    [StringLength(255, ErrorMessage = "PB-21")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "PB-22")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "PB-23")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "PB-24")]
    public Guid PreferredLanguage { get; set; }
}