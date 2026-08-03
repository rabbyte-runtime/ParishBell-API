namespace ParishBell.Core.DTOs.User;

// NOTE: One switch per push type, matching NotificationType 1..4.
// NOTE: System pushes are not optional.
public class NotificationPreferencesDto
{
    public bool Events { get; set; }
    public bool Announcements { get; set; }
    public bool MassReminders { get; set; }
    public bool FeastDays { get; set; }
}
