namespace ParishBell.Core.DTOs.Push;

public class PushNotification
{
    // NOTE: The notification title shown on the device.
    public string Title { get; set; } = string.Empty;

    // NOTE: The notification body text.
    public string Body { get; set; } = string.Empty;

    // NOTE: Optional key/value payload delivered alongside the notification for client-side routing
    // IMPORTANT: FCM requires every value to be a string.
    public IReadOnlyDictionary<string, string>? Data { get; set; }
}
