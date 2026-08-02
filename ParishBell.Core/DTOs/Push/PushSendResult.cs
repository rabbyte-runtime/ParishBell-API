namespace ParishBell.Core.DTOs.Push;

public class PushSendResult
{
    // NOTE: Device tokens FCM accepted for delivery.
    public int SuccessCount { get; set; }

    // NOTE: Device tokens FCM rejected (transient errors or stale tokens).
    public int FailureCount { get; set; }

    // NOTE: Stale tokens (unregistered / invalid) that were deleted from the store as a result of this send.
    public int PrunedTokenCount { get; set; }

    // NOTE: Nothing to send (no target tokens).
    public static PushSendResult Empty => new();
}
