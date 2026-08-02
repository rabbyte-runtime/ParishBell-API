using ParishBell.Core.DTOs.Mass;

namespace ParishBell.Core.DTOs.Location;

public class LocationDetailDto
{
    public Guid LocationId { get; set; }
    public Guid LocationTypeId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public List<LocationImageDto> Images { get; set; } = [];

    // NOTE: Dated occurrences over the requested window, identical in shape to GET /api/v1/mass/schedule - one concept, one model.
    // NOTE: Defaults to the coming week, so the profile's mass tab can render "what's on this week" without projecting dates itself.
    public List<MassOccurrenceDto> MassSchedules { get; set; } = [];

    // NOTE: The window the occurrences above were expanded over, echoed back so the client knows what it is looking at.
    public string MassFrom { get; set; } = default!;
    public string MassTo { get; set; } = default!;
    public List<LocationFeastDayDto> FeastDays { get; set; } = [];

    // NOTE: Whether the caller follows this church, so the detail sheet renders Join/Leave without a second call.
    // IMPORTANT: This endpoint is public - an anonymous caller always gets false, which is not the same as "unknown".
    public bool IsFollowing { get; set; }
}

public class LocationImageDto
{
    public Guid ImageId { get; set; }
    public string ImageUrl { get; set; } = default!;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

public class LocationFeastDayDto
{
    public Guid LocationFeastDayId { get; set; }
    public bool IsHighlighted { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsHolyDay { get; set; }
    // NOTE: True = fixed annual (use Month + Day). False = one-off (use SpecificDate).
    public bool IsRecurringAnnually { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    // NOTE: "yyyy-MM-dd" — non-null only when IsRecurringAnnually is false
    public string? SpecificDate { get; set; }
}
