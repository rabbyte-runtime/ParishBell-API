using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.User;

// NOTE: Partial update - omit a field to leave it unchanged.
// NOTE: Blank strings are rejected rather than treated as "clear".
public class UpdateProfileRequestDto
{
    [StringLength(255, MinimumLength = 2, ErrorMessage = "PB-18")]
    public string? FullName { get; set; }

    // IMPORTANT: Email accounts only - social users own their email at the provider.
    [EmailAddress(ErrorMessage = "PB-20")]
    [StringLength(255, ErrorMessage = "PB-21")]
    public string? Email { get; set; }

    // NOTE: Must reference an active row in languages, else PB-64.
    public Guid? PreferredLanguage { get; set; }
}
