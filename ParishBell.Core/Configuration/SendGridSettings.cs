namespace ParishBell.Core.Configuration;

public class SendGridSettings
{
    // NOTE: SendGrid API key - stored in user secrets
    public string ApiKey { get; set; } = string.Empty;

    // NOTE: Verified sender email address
    public string SenderEmail { get; set; } = string.Empty;

    // NOTE: Sender display name shown in email clients
    public string SenderName { get; set; } = "ParishBell Team";

    // IMPORTANT: When true, emails are logged to console instead of sent (dev/testing)
    public bool MockMode { get; set; } = false;
}
