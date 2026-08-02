using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Configuration;
using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.DTOs.Push;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class MassReminderNotificationServiceTests
{
    private readonly Mock<IMassReminderNotificationRepository> _mockRepo;
    private readonly Mock<IPushNotificationService> _mockPush;
    private readonly Mock<IMessageCache> _mockMessages;
    private readonly MassReminderNotificationService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _scheduleId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    // NOTE: Sri Lanka's offset, matching the default the job ships with.
    private const int OffsetMinutes = 330;

    private List<NotificationsLog> _captured = [];

    public MassReminderNotificationServiceTests()
    {
        _mockRepo = new Mock<IMassReminderNotificationRepository>();
        _mockPush = new Mock<IPushNotificationService>();
        _mockMessages = new Mock<IMessageCache>();

        _mockMessages
            .Setup(m => m.GetText(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("{0} starts in {1} minutes.");

        _mockRepo
            .Setup(r => r.GetAlreadyNotifiedAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockRepo
            .Setup(r => r.AddLogsAsync(It.IsAny<IReadOnlyCollection<NotificationsLog>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<NotificationsLog> logs, CancellationToken _) => _captured = [.. logs])
            .Returns(Task.CompletedTask);

        _mockRepo
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _service = new MassReminderNotificationService(
            _mockRepo.Object,
            _mockPush.Object,
            _mockMessages.Object,
            new MassReminderPushSettings { LocalUtcOffsetMinutes = OffsetMinutes, LookbackMinutes = 15 });
    }

    // NOTE: Builds a reminder whose fire time sits `minutesAgo` in the past relative to now, at the church's wall clock.
    private DueMassReminder MakeReminderDueMinutesAgo(int minutesAgo, int minutesBefore = 30)
    {
        var localNow = DateTime.UtcNow.AddMinutes(OffsetMinutes);
        var fireAt = localNow.AddMinutes(-minutesAgo);
        var massAt = fireAt.AddMinutes(minutesBefore);

        return new DueMassReminder
        {
            UserId = _userId,
            LanguageCode = "en",
            MinutesBefore = minutesBefore,
            ScheduleId = _scheduleId,
            LocationId = _locationId,
            LocationName = "St. Anthony's Shrine",
            Label = "Sinhala Mass",
            DayOfWeek = (int)massAt.DayOfWeek,
            MassTime = TimeOnly.FromDateTime(massAt)
        };
    }

    private void SetupReminders(params DueMassReminder[] reminders) =>
        _mockRepo
            .Setup(r => r.GetActiveRemindersAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. reminders]);

    // IMPORTANT: TEST 1 - A reminder whose fire time has just passed is queued, dated to the mass it is for
    [Fact]
    public async Task ProcessDue_WhenFireTimeJustPassed_QueuesIt()
    {
        // NOTE: Arrange
        var reminder = MakeReminderDueMinutesAgo(2);
        SetupReminders(reminder);

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(1, result.Enqueued);
        var log = Assert.Single(_captured);
        Assert.Equal(_userId, log.UserId);
        Assert.Equal(_scheduleId, log.ReferenceId);
        Assert.Equal("St. Anthony's Shrine", log.Title);
        Assert.Equal("Sinhala Mass starts in 30 minutes.", log.Body);
        Assert.False(log.IsSent);
        Assert.NotNull(log.OccurrenceDate);
    }

    // IMPORTANT: TEST 2 - A fire time still ahead of us is not due yet
    [Fact]
    public async Task ProcessDue_WhenFireTimeIsStillAhead_QueuesNothing()
    {
        // NOTE: Arrange — due in ten minutes, not ten minutes ago
        SetupReminders(MakeReminderDueMinutesAgo(-10));

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(0, result.Enqueued);
        _mockRepo.Verify(r => r.AddLogsAsync(It.Is<IReadOnlyCollection<NotificationsLog>>(l => l.Count > 0), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 3 - A fire time older than the lookback is stale and silently dropped, not sent late
    [Fact]
    public async Task ProcessDue_WhenFireTimeIsOlderThanLookback_SkipsIt()
    {
        // NOTE: Arrange — the window reaches back 15 minutes
        SetupReminders(MakeReminderDueMinutesAgo(40));

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(0, result.Enqueued);
    }

    // IMPORTANT: TEST 4 - A reminder already logged for that day is not queued again, however often the job polls
    [Fact]
    public async Task ProcessDue_WhenAlreadyNotifiedForThatDay_SkipsIt()
    {
        // NOTE: Arrange
        var reminder = MakeReminderDueMinutesAgo(2);
        SetupReminders(reminder);

        var localNow = DateTime.UtcNow.AddMinutes(OffsetMinutes);
        var occurrence = DateOnly.FromDateTime(localNow.AddMinutes(-2).AddMinutes(reminder.MinutesBefore));

        _mockRepo
            .Setup(r => r.GetAlreadyNotifiedAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([(_userId, _scheduleId, occurrence)]);

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(0, result.Enqueued);
    }

    // IMPORTANT: TEST 5 - A special mass outside its own window never fires, even when the weekday matches
    [Fact]
    public async Task ProcessDue_SpecialMassOutsideItsWindow_SkipsIt()
    {
        // NOTE: Arrange
        var reminder = MakeReminderDueMinutesAgo(2);
        reminder.IsSpecial = true;
        reminder.ValidFrom = new DateOnly(2020, 1, 1);
        reminder.ValidTo = new DateOnly(2020, 12, 31);
        SetupReminders(reminder);

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(0, result.Enqueued);
    }

    // IMPORTANT: TEST 6 - A special mass inside its window fires like any other
    [Fact]
    public async Task ProcessDue_SpecialMassInsideItsWindow_QueuesIt()
    {
        // NOTE: Arrange
        var reminder = MakeReminderDueMinutesAgo(2);
        reminder.IsSpecial = true;
        reminder.ValidFrom = new DateOnly(2020, 1, 1);
        reminder.ValidTo = new DateOnly(2099, 12, 31);
        SetupReminders(reminder);

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(1, result.Enqueued);
    }

    // IMPORTANT: TEST 7 - A mass on another weekday is not due today
    [Fact]
    public async Task ProcessDue_WhenScheduleIsForAnotherWeekday_SkipsIt()
    {
        // NOTE: Arrange
        var reminder = MakeReminderDueMinutesAgo(2);
        reminder.DayOfWeek = (reminder.DayOfWeek + 3) % 7;
        SetupReminders(reminder);

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(0, result.Enqueued);
    }

    // IMPORTANT: TEST 8 - Delivery carries the ids the client deep-links on, and marks the row sent
    [Fact]
    public async Task ProcessDue_DeliversPendingWithDeepLinkPayload()
    {
        // NOTE: Arrange
        SetupReminders();

        var notificationId = Guid.NewGuid();
        var occurrence = new DateOnly(2026, 8, 9);

        _mockRepo
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingNotification
            {
                NotificationId = notificationId,
                UserId = _userId,
                Title = "St. Anthony's Shrine",
                Body = "Sinhala Mass starts in 30 minutes.",
                ReferenceId = _scheduleId,
                LocationId = _locationId,
                OccurrenceDate = occurrence
            }]);

        PushNotification? sent = null;
        _mockPush
            .Setup(p => p.SendToUserAsync(_userId, It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, PushNotification n, CancellationToken _) => sent = n)
            .ReturnsAsync(new PushSendResult { SuccessCount = 1 });

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(1, result.Delivered);
        Assert.NotNull(sent?.Data);
        Assert.Equal("massReminder", sent.Data["type"]);
        Assert.Equal(_scheduleId.ToString(), sent.Data["scheduleId"]);
        Assert.Equal(_locationId.ToString(), sent.Data["locationId"]);
        Assert.Equal("2026-08-09", sent.Data["date"]);
        _mockRepo.Verify(r => r.MarkSentAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(notificationId)), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 9 - A hard delivery failure is left queued for the next poll rather than marked sent
    [Fact]
    public async Task ProcessDue_WhenDeliveryFails_LeavesItQueued()
    {
        // NOTE: Arrange
        SetupReminders();

        _mockRepo
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingNotification { NotificationId = Guid.NewGuid(), UserId = _userId, Title = "T", Body = "B" }]);

        _mockPush
            .Setup(p => p.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushSendResult { FailureCount = 1 });

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(0, result.Delivered);
        Assert.Equal(1, result.Failed);
        _mockRepo.Verify(r => r.MarkSentAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 10 - A malformed template must still produce a usable body rather than throwing
    [Fact]
    public async Task ProcessDue_WithMalformedTemplate_FallsBackToTheLabel()
    {
        // NOTE: Arrange — an unbalanced brace is what an unseeded or mistyped row would look like
        _mockMessages.Setup(m => m.GetText(It.IsAny<string>(), It.IsAny<string>())).Returns("{0} starts in {1 minutes.");
        SetupReminders(MakeReminderDueMinutesAgo(2));

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(1, result.Enqueued);
        Assert.Equal("Sinhala Mass", Assert.Single(_captured).Body);
    }
}
