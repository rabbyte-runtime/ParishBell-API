namespace ParishBell.Core.DTOs.Location;

public class AnnouncementPageDto
{
    public List<AnnouncementDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

public class AnnouncementDto
{
    public Guid AnnouncementId { get; set; }
    public Guid LocationId { get; set; }
    // NOTE: "Audio" | "Video" — drives waveform vs video player on the client
    public string MediaType { get; set; } = default!;
    // NOTE: Short-lived Azure Blob SAS URL baked in at upload time
    public string MediaUrl { get; set; } = default!;
    // NOTE: Video poster / audio cover art — null when none
    public string? ThumbnailUrl { get; set; }
    public int DurationSeconds { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    // NOTE: ISO-8601 UTC — channel ordering + "posted 2h ago"
    public string CreatedAt { get; set; } = default!;
    // NOTE: ISO-8601 UTC — drives the "time remaining" countdown
    public string ExpiresAt { get; set; } = default!;
}
