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

    // NOTE: Weekly patterns, not dates - the service expands them like the calendar does.
    List<MassSchedulePatternResult> Schedules,
    List<FeastDayResult> FeastDays,

    // NOTE: Always false for an anonymous caller, since the endpoint is public.
    bool IsFollowing,

    // NOTE: The pin colour of this church type, so the sheet matches the tapped pin.
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
