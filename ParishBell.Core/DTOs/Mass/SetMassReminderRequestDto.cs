using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.Mass;

public class SetMassReminderRequestDto
{
    // NOTE: The mass being remembered. An unknown or hidden one is a 404 (PB-75).
    public Guid ScheduleId { get; set; }

    // NOTE: Push fires at (massTime - minutesBefore). A day ahead is the ceiling.
    [Range(1, 1440, ErrorMessage = "PB-76")]
    public int MinutesBefore { get; set; }
}
