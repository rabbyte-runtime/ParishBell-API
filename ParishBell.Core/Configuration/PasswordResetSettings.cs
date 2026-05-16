namespace ParishBell.Core.Configuration;

public class PasswordResetSettings
{
    // NOTE: How long the reset code is valid for - 15 minutes
    public int CodeExpiryMinutes { get; set; }

    // NOTE: Maximum reset requests per email per hour
    public int MaxRequestsPerHour { get; set; }
}
