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
    public List<MassScheduleDto> MassSchedules { get; set; } = [];
    public List<LocationFeastDayDto> FeastDays { get; set; } = [];
}

public class LocationImageDto
{
    public Guid ImageId { get; set; }
    public string ImageUrl { get; set; } = default!;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

public class MassScheduleDto
{
    public Guid ScheduleId { get; set; }
    // NOTE: 0=Sunday, 1=Monday ... 6=Saturday
    public int DayOfWeek { get; set; }
    // NOTE: "HH:mm" 24-hour format
    public string MassTime { get; set; } = default!;
    public bool IsSpecial { get; set; }
    // NOTE: "yyyy-MM-dd" — non-null only when IsSpecial is true
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
    public string? Label { get; set; }
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
