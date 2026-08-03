namespace ParishBell.Core.Configuration;

public class FeastDayPushSettings
{
    // NOTE: Master switch for the background feast day job.
    public bool Enabled { get; set; } = true;

    // NOTE: Feast days are whole-day events, so this polls far less often than the minute-precise reminder job.
    public int PollIntervalSeconds { get; set; } = 900;

    // NOTE: Local time of day the notification goes out - 07:00 at the church, not at the server.
    public int SendAtLocalHour { get; set; } = 7;

    // IMPORTANT: How late a missed send is still worth making. Covers a restart; past this the day is half over and a
    // IMPORTANT:  "today is the feast" push has lost its point.
    public int LookbackHours { get; set; } = 6;

    // NOTE: Max notifications delivered per poll iteration.
    public int DeliverBatchSize { get; set; } = 500;

    // IMPORTANT: Same reasoning as the reminder job - liturgical dates are calendar dates at the church, so the
    // IMPORTANT:  server's UTC clock has to be shifted before "is it that day yet" means anything. 330 = Sri Lanka.
    public int LocalUtcOffsetMinutes { get; set; } = 330;
}
