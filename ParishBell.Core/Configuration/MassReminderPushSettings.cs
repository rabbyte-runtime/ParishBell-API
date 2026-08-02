namespace ParishBell.Core.Configuration;

public class MassReminderPushSettings
{
    // NOTE: Master switch for the background reminder job.
    public bool Enabled { get; set; } = true;

    // NOTE: How often the job looks for reminders that have come due. Reminders are minute-precision, so polling
    //       slower than this would drift the pushes noticeably late.
    public int PollIntervalSeconds { get; set; } = 60;

    // IMPORTANT: How far back a missed fire time is still worth sending. Covers a restart or a slow poll without
    //            blasting a backlog - anything older than this is stale and silently skipped.
    public int LookbackMinutes { get; set; } = 15;

    // NOTE: Max notifications delivered per poll iteration.
    public int DeliverBatchSize { get; set; } = 500;

    // IMPORTANT: mass_time is wall-clock time at the church, with no timezone in the column. The server runs on UTC,
    // IMPORTANT:  so without this offset a 06:30 mass would be notified 5.5 hours late. Sri Lanka is UTC+05:30 and has
    // IMPORTANT:  no daylight saving, which is why a fixed offset is safe here rather than a timezone database.
    public int LocalUtcOffsetMinutes { get; set; } = 330;
}
