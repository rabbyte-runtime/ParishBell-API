namespace ParishBell.Core.DTOs.Common;

public record UserProfileResult(
    Guid UserId,
    string FullName,
    string Email,
    string? ProfileImageUrl,
    short AuthProvider,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    Guid PreferredLanguage,
    string PreferredLanguageCode,
    string PreferredLanguageName,
    string PreferredLanguageNativeName
);
