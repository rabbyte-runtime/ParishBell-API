using System.Runtime.CompilerServices;
using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Configuration;
using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.DTOs.Push;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class AnnouncementNotificationServiceTests
{
    private readonly Mock<IAnnouncementNotificationRepository> _repo = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly AnnouncementPushSettings _settings = new() { LookbackMinutes = 60, DeliverBatchSize = 500 };
    private readonly AnnouncementNotificationService _service;

    public AnnouncementNotificationServiceTests()
    {
        _service = new AnnouncementNotificationService(_repo.Object, _push.Object, _settings);

        // NOTE: Default the two "list" reads to empty so each test only sets up the phase it exercises.
        _repo.Setup(r => r.GetAnnouncementsToNotifyAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingAnnouncement>());
        _repo.Setup(r => r.GetPendingAsync(It.IsAny<short>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingNotification>());
    }

    // IMPORTANT: TEST 1 - Enqueue writes one localized log row per follower, tagged Announcement + reference id
    [Fact]
    public async Task Enqueue_CreatesLocalizedLogPerFollower()
    {
        var announcementId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var enUser = Guid.NewGuid();
        var taUser = Guid.NewGuid();

        _repo.Setup(r => r.GetAnnouncementsToNotifyAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Announcement(announcementId, locationId,
                ("en", "Mass moved", "9am today"),
                ("ta", "திருப்பலி மாற்றம்", "இன்று 9 மணி"))]);

        _repo.Setup(r => r.GetFollowersWithoutLogAsync(announcementId, locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Recipient(enUser, "en"), Recipient(taUser, "ta")]);

        var captured = CaptureAddedLogs();

        var result = await _service.ProcessPendingAsync();

        Assert.Equal(2, result.Enqueued);
        Assert.NotNull(captured.Value);
        Assert.Equal(2, captured.Value!.Count);
        Assert.All(captured.Value, l => Assert.Equal(announcementId, l.ReferenceId));
        Assert.All(captured.Value, l => Assert.Equal((short)2, l.Type)); // 2 = Announcement
        Assert.All(captured.Value, l => Assert.False(l.IsSent));
        Assert.Contains(captured.Value, l => l.UserId == enUser && l.Title == "Mass moved");
        Assert.Contains(captured.Value, l => l.UserId == taUser && l.Title == "திருப்பலி மாற்றம்");
    }

    // IMPORTANT: TEST 2 - Only followers without an existing log are queried; empty recipients => no insert
    [Fact]
    public async Task Enqueue_NoNewFollowers_DoesNotInsert()
    {
        var announcementId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        _repo.Setup(r => r.GetAnnouncementsToNotifyAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Announcement(announcementId, locationId, ("en", "Hi", "There"))]);
        _repo.Setup(r => r.GetFollowersWithoutLogAsync(announcementId, locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _service.ProcessPendingAsync();

        Assert.Equal(0, result.Enqueued);
        _repo.Verify(r => r.AddLogsAsync(It.IsAny<IReadOnlyCollection<NotificationsLog>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 3 - Delivery marks only successes sent; hard failures are left for retry
    [Fact]
    public async Task Deliver_MarksSucceededSent_LeavesFailuresForRetry()
    {
        var okId = Guid.NewGuid();
        var failId = Guid.NewGuid();
        var okUser = Guid.NewGuid();
        var failUser = Guid.NewGuid();

        _repo.Setup(r => r.GetPendingAsync(It.IsAny<short>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Pending(okId, okUser), Pending(failId, failUser)]);

        _push.Setup(p => p.SendToUserAsync(okUser, It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushSendResult { SuccessCount = 1 });
        _push.Setup(p => p.SendToUserAsync(failUser, It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushSendResult { FailureCount = 1 });

        var marked = CaptureMarkedSent();

        var result = await _service.ProcessPendingAsync();

        Assert.Equal(1, result.Delivered);
        Assert.Equal(1, result.Failed);
        Assert.NotNull(marked.Value);
        Assert.Single(marked.Value!);
        Assert.Contains(okId, marked.Value!);
        Assert.DoesNotContain(failId, marked.Value!);
    }

    // IMPORTANT: TEST 4 - A user with no devices (0 success, 0 failure) is terminal, not retried forever
    [Fact]
    public async Task Deliver_NoDevices_CountsAsDelivered()
    {
        var id = Guid.NewGuid();
        var user = Guid.NewGuid();

        _repo.Setup(r => r.GetPendingAsync(It.IsAny<short>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Pending(id, user)]);
        _push.Setup(p => p.SendToUserAsync(user, It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushSendResult.Empty);

        var marked = CaptureMarkedSent();

        var result = await _service.ProcessPendingAsync();

        Assert.Equal(1, result.Delivered);
        Assert.Contains(id, marked.Value!);
    }

    // IMPORTANT: TEST 5 - Localization falls back preferred -> English, and -> generic when nothing usable
    [Fact]
    public async Task Enqueue_LocalizationFallback()
    {
        var annA = Guid.NewGuid();
        var locA = Guid.NewGuid();
        var annB = Guid.NewGuid();
        var locB = Guid.NewGuid();
        var siUser = Guid.NewGuid();   // follows A (only an en title exists) => English fallback
        var taUser = Guid.NewGuid();   // follows B (no translations at all)   => generic ta

        _repo.Setup(r => r.GetAnnouncementsToNotifyAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Announcement(annA, locA, ("en", "Only English", null)),
                Announcement(annB, locB)
            ]);
        _repo.Setup(r => r.GetFollowersWithoutLogAsync(annA, locA, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Recipient(siUser, "si")]);
        _repo.Setup(r => r.GetFollowersWithoutLogAsync(annB, locB, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Recipient(taUser, "ta")]);

        var captured = CaptureAddedLogs();

        await _service.ProcessPendingAsync();

        Assert.Equal("Only English", captured.Value!.First(l => l.UserId == siUser).Title);    // en fallback
        Assert.Equal("புதிய அறிவிப்பு", captured.Value!.First(l => l.UserId == taUser).Title);  // generic ta
    }

    // ---- helpers ----

    private static PendingAnnouncement Announcement(Guid id, Guid locationId, params (string Lang, string? Title, string? Caption)[] translations) => new()
    {
        AnnouncementId = id,
        LocationId = locationId,
        CreatedAt = DateTime.UtcNow,
        Translations = translations
            .Select(t => new AnnouncementTranslationText { LanguageCode = t.Lang, Title = t.Title, Caption = t.Caption })
            .ToList()
    };

    private static NotificationRecipient Recipient(Guid userId, string lang) => new() { UserId = userId, LanguageCode = lang };

    private static PendingNotification Pending(Guid id, Guid userId) => new()
    {
        NotificationId = id,
        UserId = userId,
        Title = "t",
        Body = "b",
        ReferenceId = Guid.NewGuid()
    };

    private StrongBox<List<NotificationsLog>?> CaptureAddedLogs()
    {
        // NOTE: Accumulate across calls — one AddLogs call happens per announcement.
        var box = new StrongBox<List<NotificationsLog>?>(new List<NotificationsLog>());
        _repo.Setup(r => r.AddLogsAsync(It.IsAny<IReadOnlyCollection<NotificationsLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<NotificationsLog>, CancellationToken>((logs, _) => box.Value!.AddRange(logs))
            .Returns(Task.CompletedTask);
        return box;
    }

    private StrongBox<IReadOnlyCollection<Guid>?> CaptureMarkedSent()
    {
        var box = new StrongBox<IReadOnlyCollection<Guid>?>(null);
        _repo.Setup(r => r.MarkSentAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>, DateTime, CancellationToken>((ids, _, _) => box.Value = ids)
            .Returns(Task.CompletedTask);
        return box;
    }
}
