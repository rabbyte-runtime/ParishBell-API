namespace ParishBell.Core.DTOs.User;

// NOTE: Partial update - send only the switches that moved. Omitted (or null) leaves that preference alone.
public class UpdateNotificationPreferencesRequestDto
{
    public bool? Events { get; set; }
    public bool? Announcements { get; set; }
    public bool? MassReminders { get; set; }
    public bool? FeastDays { get; set; }
}
