namespace ParishBell.Core.DTOs.Push;

public class PushNotification
{
    // NOTE: The notification title shown on the device.
    public string Title { get; set; } = string.Empty;

    // NOTE: The notification body text.
    public string Body { get; set; } = string.Empty;

    // NOTE: Optional key/value payload delivered alongside the notification for client-side routing
    //       (e.g. { "type": "announcement", "id": "..." }). FCM requires all values to be strings.
    public IReadOnlyDictionary<string, string>? Data { get; set; }
}
