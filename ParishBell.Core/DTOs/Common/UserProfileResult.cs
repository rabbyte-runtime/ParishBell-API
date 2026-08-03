namespace ParishBell.Core.DTOs.Common;

public record UserProfileResult(
    Guid UserId,
    string FullName,
    string Email,
    // NOTE: The Google/Apple account photo. Only used when the user has not uploaded one of their own.
    string? ProfileImageUrl,

    // NOTE: Blob name of an uploaded photo, not a URL - the read URL is a SAS minted per response.
    string? ProfilePhotoBlob,

    short AuthProvider,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    Guid PreferredLanguage,
    string PreferredLanguageCode,
    string PreferredLanguageName,
    string PreferredLanguageNativeName,
    bool NotifyEvents,
    bool NotifyAnnouncements,
    bool NotifyMassReminders,
    bool NotifyFeastDays
);
