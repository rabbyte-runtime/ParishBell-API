using System.ComponentModel.DataAnnotations;

namespace ParishBell.Core.DTOs.Mass;

public class SetMassReminderRequestDto
{
    // NOTE: The mass being remembered. An unknown or hidden one is a 404 (PB-75), which also covers an empty Guid.
    public Guid ScheduleId { get; set; }

    // NOTE: Push fires at (massTime - minutesBefore). The UI offers 15/30/60 plus a custom value; a day ahead is the ceiling.
    [Range(1, 1440, ErrorMessage = "PB-76")]
    public int MinutesBefore { get; set; }
}
