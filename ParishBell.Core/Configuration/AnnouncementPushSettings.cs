namespace ParishBell.Core.Configuration;

public class AnnouncementPushSettings
{
    // NOTE: Master switch for the background push job.
    public bool Enabled { get; set; } = true;

    // NOTE: How often the outbox job polls for new/undelivered announcement notifications.
    public int PollIntervalSeconds { get; set; } = 60;

    // IMPORTANT: Only announcements created within this window are eligible. Prevents a backlog blast
    //            on first deploy or after downtime, and keeps "new announcement" pushes timely.
    public int LookbackMinutes { get; set; } = 60;

    // NOTE: Max notifications delivered per poll iteration.
    public int DeliverBatchSize { get; set; } = 500;
}
