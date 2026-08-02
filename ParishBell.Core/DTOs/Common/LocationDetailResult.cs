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
    List<MassScheduleResult> Schedules,
    List<FeastDayResult> FeastDays
);

public record LocationImageResult(Guid ImageId, string ImageUrl, bool IsPrimary, int SortOrder);

public record MassScheduleResult(
    Guid ScheduleId,
    int DayOfWeek,
    TimeOnly MassTime,
    bool IsSpecial,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string? Label
);

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
