namespace ParishBell.Core.Configuration;

public class GoogleAuthSettings
{
    // NOTE: Google OAuth Client IDs - support multiple (Web, iOS, Android)
    // IMPORTANT: ID tokens issued to any of these client IDs are accepted
    public string[] ClientIds { get; set; } = [];
}