using ParishBell.Core.Enums;

namespace ParishBell.Core.DTOs.User;

public class UserProfileDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }

    // NOTE: 1=Email, 2=Google, 3=Apple. Lets the app hide password settings.
    public AuthProvider AuthProvider { get; set; }

    // NOTE: Language id plus resolved names, so the setting needs no second lookup.
    public Guid PreferredLanguage { get; set; }
    public string PreferredLanguageCode { get; set; } = string.Empty;
    public string PreferredLanguageName { get; set; } = string.Empty;
    public string PreferredLanguageNativeName { get; set; } = string.Empty;

    // NOTE: ISO-8601 UTC — "member since" on the profile screen
    public string CreatedAt { get; set; } = default!;
    // NOTE: ISO-8601 UTC — null until the account's first sign-in completes
    public string? LastLoginAt { get; set; }
}
