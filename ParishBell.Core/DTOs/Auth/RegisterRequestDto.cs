using System.ComponentModel.DataAnnotations;
using ParishBell.Core.Enums;

namespace ParishBell.Core.DTOs.Auth;

public class RegisterRequestDto
{
    // NOTE: Provider numeric value - 1=Email, 2=Google
    [Required(ErrorMessage = "PB-28")]
    public AuthProvider Provider { get; set; }

    // NOTE: Full name - required for Email, optional for Google (Google's name used as fallback)
    [StringLength(255, MinimumLength = 2, ErrorMessage = "PB-18")]
    public string? FullName { get; set; }

    // NOTE: Email - required only for Email provider
    [EmailAddress(ErrorMessage = "PB-20")]
    [StringLength(255, ErrorMessage = "PB-21")]
    public string? Email { get; set; }

    // NOTE: Password - required only for Email provider
    public string? Password { get; set; }

    // NOTE: Password confirmation - required only for Email provider
    public string? ConfirmPassword { get; set; }

    // NOTE: Google/Apple ID token - required for social providers
    public string? IdToken { get; set; }

    // NOTE: Preferred language is required for all providers
    [Required(ErrorMessage = "PB-24")]
    public Guid PreferredLanguage { get; set; }
}