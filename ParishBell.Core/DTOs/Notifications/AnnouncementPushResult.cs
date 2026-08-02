namespace ParishBell.Core.DTOs.Notifications;

// NOTE: Outcome of one announcement-push processing pass (one poll iteration).
public readonly record struct AnnouncementPushResult(int Enqueued, int Delivered, int Failed);
