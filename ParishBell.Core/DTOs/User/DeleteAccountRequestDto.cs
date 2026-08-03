using System.ComponentModel.DataAnnotations;
using ParishBell.Core.Enums;

namespace ParishBell.Core.DTOs.User;

// IMPORTANT: Deletion is irreversible, so the caller re-proves who they are.
// NOTE: They must use the same credential they signed up with.
public class DeleteAccountRequestDto
{
    // NOTE: Provider numeric value - 1=Email, 2=Google, 3=Apple. Must match the account's own provider.
    [Required(ErrorMessage = "PB-28")]
    public AuthProvider Provider { get; set; }

    // NOTE: Current password - required for Email accounts.
    public string? Password { get; set; }

    // NOTE: Freshly issued Google ID token - required for Google accounts.
    public string? IdToken { get; set; }
}
