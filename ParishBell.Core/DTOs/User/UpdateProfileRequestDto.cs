using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.User;

// NOTE: Partial update - omit a field (or send null) to leave it unchanged. Blank strings are rejected, not treated as "clear".
public class UpdateProfileRequestDto
{
    [StringLength(255, MinimumLength = 2, ErrorMessage = "PB-18")]
    public string? FullName { get; set; }

    // IMPORTANT: Email-provider accounts only. Google/Apple users own their email at the provider - 403 if they try.
    [EmailAddress(ErrorMessage = "PB-20")]
    [StringLength(255, ErrorMessage = "PB-21")]
    public string? Email { get; set; }

    // NOTE: Must reference an active row in languages, else PB-64.
    public Guid? PreferredLanguage { get; set; }
}
