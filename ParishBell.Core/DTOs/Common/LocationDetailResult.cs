namespace ParishBell.Core.DTOs.Common;

public record LocationDetailResult(
    Guid LocationId,
    Guid LocationTypeId,
    decimal Latitude,
    decimal Longitude,
    string? Phone,
    string? Email,
    string? Website,
    string Name,
    string? Description,
    string? Address,
    List<LocationImageResult> Images,

    // NOTE: Weekly patterns, not dates - the service expands them, exactly as the followed-churches calendar does.
    List<MassSchedulePatternResult> Schedules,
    List<FeastDayResult> FeastDays,

    // NOTE: Always false for an anonymous caller - the endpoint is public, so "not following" and "nobody asked" look the same.
    bool IsFollowing,

    // NOTE: The map pin colour of this church's type, carried through so the detail sheet matches the pin the user tapped.
    string? PinColorHex
);

public record LocationImageResult(Guid ImageId, string ImageUrl, bool IsPrimary, int SortOrder);

public record FeastDayResult(
    Guid LocationFeastDayId,
    bool IsHighlighted,
    bool IsHolyDay,
    bool IsRecurringAnnually,
    int? Month,
    int? Day,
    DateOnly? SpecificDate,
    string Title,
    string? Description
);
