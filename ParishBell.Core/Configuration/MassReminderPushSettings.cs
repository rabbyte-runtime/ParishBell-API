namespace ParishBell.Core.Configuration;

public class MassReminderPushSettings
{
    // NOTE: Master switch for the background reminder job.
    public bool Enabled { get; set; } = true;

    // NOTE: How often the job looks for due reminders.
    // NOTE: Reminders are minute-precision, so a slower poll drifts pushes late.
    public int PollIntervalSeconds { get; set; } = 60;

    // IMPORTANT: How far back a missed fire time is still worth sending.
    // NOTE: Covers a restart without blasting a backlog; older is stale and skipped.
    public int LookbackMinutes { get; set; } = 15;

    // NOTE: Max notifications delivered per poll iteration.
    public int DeliverBatchSize { get; set; } = 500;

    // IMPORTANT: mass_time is wall-clock at the church, with no timezone in the column.
    // IMPORTANT: Without this offset a 06:30 mass would be notified 5.5 hours late.
    // NOTE: Sri Lanka is UTC+05:30 with no daylight saving, so a fixed offset is safe.
    public int LocalUtcOffsetMinutes { get; set; } = 330;
}
