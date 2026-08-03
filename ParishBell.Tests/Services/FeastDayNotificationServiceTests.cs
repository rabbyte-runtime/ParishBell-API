using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Configuration;
using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.DTOs.Push;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class FeastDayNotificationServiceTests
{
    private readonly Mock<IFeastDayNotificationRepository> _mockRepo;
    private readonly Mock<IPushNotificationService> _mockPush;
    private readonly Mock<IMessageCache> _mockMessages;
    private readonly FeastDayPushSettings _settings;
    private readonly FeastDayNotificationService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _feastDayId = Guid.NewGuid();
    private readonly Guid _calendarId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _languageId = Guid.NewGuid();

    private const int OffsetMinutes = 330;

    private List<NotificationsLog> _captured = [];

    public FeastDayNotificationServiceTests()
    {
        _mockRepo = new Mock<IFeastDayNotificationRepository>();
        _mockPush = new Mock<IPushNotificationService>();
        _mockMessages = new Mock<IMessageCache>();

        var localNow = DateTime.UtcNow.AddMinutes(OffsetMinutes);

        // NOTE: The window is anchored to the local hour, aimed at now to stay time-independent.
        _settings = new FeastDayPushSettings
        {
            LocalUtcOffsetMinutes = OffsetMinutes,
            SendAtLocalHour = localNow.Hour,
            LookbackHours = 6
        };

        _mockMessages.Setup(m => m.GetText(It.IsAny<string>(), It.IsAny<string>())).Returns("Feast day");

        _mockRepo
            .Setup(r => r.GetRecipientsWithoutLogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new FeastDayRecipient(_userId, _languageId, "en")]);

        _mockRepo
            .Setup(r => r.GetTranslationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (string Title, string? Description)>
            {
                [_languageId] = ("The Assumption", "Holy Day of Obligation")
            });

        _mockRepo
            .Setup(r => r.AddLogsAsync(It.IsAny<IReadOnlyCollection<NotificationsLog>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<NotificationsLog> logs, CancellationToken _) => _captured = [.. logs])
            .Returns(Task.CompletedTask);

        _mockRepo
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _service = new FeastDayNotificationService(_mockRepo.Object, _mockPush.Object, _mockMessages.Object, _settings);
    }

    private DateOnly LocalToday() => DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(OffsetMinutes));

    private void SetupFeasts(params DueFeastDay[] feasts) =>
        _mockRepo
            .Setup(r => r.GetPinnedFeastDaysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. feasts]);

    private DueFeastDay RecurringToday()
    {
        var today = LocalToday();
        return new DueFeastDay
        {
            LocationFeastDayId = _feastDayId,
            CalendarId = _calendarId,
            LocationId = _locationId,
            IsRecurringAnnually = true,
            Month = today.Month,
            Day = today.Day
        };
    }

    // IMPORTANT: TEST 1 - A recurring feast today is queued, dated and localised
    [Fact]
    public async Task ProcessDue_RecurringFeastToday_QueuesIt()
    {
        // NOTE: Arrange
        SetupFeasts(RecurringToday());

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(1, result.Enqueued);
        var log = Assert.Single(_captured);
        Assert.Equal(_userId, log.UserId);
        Assert.Equal(_feastDayId, log.ReferenceId);
        Assert.Equal(LocalToday(), log.OccurrenceDate);
        Assert.Equal("The Assumption", log.Title);
        Assert.Equal("Holy Day of Obligation", log.Body);
        Assert.False(log.IsSent);
    }

    // IMPORTANT: TEST 2 - A fixed feast is queued only on its own date
    [Fact]
    public async Task ProcessDue_FixedFeastOnAnotherDate_QueuesNothing()
    {
        // NOTE: Arrange
        SetupFeasts(new DueFeastDay
        {
            LocationFeastDayId = _feastDayId,
            CalendarId = _calendarId,
            LocationId = _locationId,
            IsRecurringAnnually = false,
            SpecificDate = LocalToday().AddDays(3)
        });

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(0, result.Enqueued);
    }

    // IMPORTANT: TEST 3 - A recurring feast on a different month/day is not today's
    [Fact]
    public async Task ProcessDue_RecurringFeastOnAnotherDay_QueuesNothing()
    {
        // NOTE: Arrange
        var other = LocalToday().AddDays(5);
        SetupFeasts(new DueFeastDay
        {
            LocationFeastDayId = _feastDayId,
            CalendarId = _calendarId,
            LocationId = _locationId,
            IsRecurringAnnually = true,
            Month = other.Month,
            Day = other.Day
        });

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(0, result.Enqueued);
    }

    // IMPORTANT: TEST 4 - Nothing goes out before the configured local hour
    [Fact]
    public async Task ProcessDue_BeforeTheSendHour_QueuesNothing()
    {
        // NOTE: Arrange — aim the window at an hour that has not arrived yet
        var localNow = DateTime.UtcNow.AddMinutes(OffsetMinutes);
        _settings.SendAtLocalHour = (localNow.Hour + 2) % 24;
        SetupFeasts(RecurringToday());

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(0, result.Enqueued);
    }

    // IMPORTANT: TEST 5 - Recipients already logged that day are excluded, so nothing queues
    [Fact]
    public async Task ProcessDue_WhenEveryoneAlreadyNotified_QueuesNothing()
    {
        // NOTE: Arrange
        SetupFeasts(RecurringToday());
        _mockRepo
            .Setup(r => r.GetRecipientsWithoutLogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(0, result.Enqueued);
    }

    // IMPORTANT: TEST 6 - A feast with no translation still gets a usable push
    [Fact]
    public async Task ProcessDue_WithNoTranslations_FallsBackToTheCodedTitle()
    {
        // NOTE: Arrange
        SetupFeasts(RecurringToday());
        _mockRepo
            .Setup(r => r.GetTranslationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // NOTE: Act
        var result = await _service.ProcessDueAsync();

        // NOTE: Assert
        Assert.Equal(1, result.Enqueued);
        Assert.Equal("Feast day", Assert.Single(_captured).Title);
    }

    // IMPORTANT: TEST 7 - A translation in another language is used rather than sending nothing
    [Fact]
    public async Task ProcessDue_WithoutTheRecipientsLanguage_UsesWhateverExists()
    {
        // NOTE: Arrange
        SetupFeasts(RecurringToday());
        _mockRepo
            .Setup(r => r.GetTranslationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (string Title, string? Description)>
            {
                [Guid.NewGuid()] = ("Assumption of Mary", null)
            });

        // NOTE: Act
        await _service.ProcessDueAsync();

        // NOTE: Assert — body falls back to the title when the entry has no description
        var log = Assert.Single(_captured);
        Assert.Equal("Assumption of Mary", log.Title);
        Assert.Equal("Assumption of Mary", log.Body);
    }

    // IMPORTANT: TEST 8 - Delivery carries the ids the client deep-links on, and marks the row sent
    [Fact]
    public async Task ProcessDue_DeliversPendingWithDeepLinkPayload()
    {
        // NOTE: Arrange
        SetupFeasts();

        var notificationId = Guid.NewGuid();
        var occurrence = new DateOnly(2026, 8, 15);

        _mockRepo
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PendingNotification
            {
                NotificationId = notificationId,
                UserId = _userId,
                Title = "The Assumption",
                Body = "Holy Day of Obligation",
                ReferenceId = _feastDayId,
                LocationId = _locationId,
                CalendarId = _calendarId,
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
        Assert.Equal("feastDay", sent.Data["type"]);
        Assert.Equal(_calendarId.ToString(), sent.Data["calendarId"]);
        Assert.Equal(_locationId.ToString(), sent.Data["locationId"]);
        Assert.Equal("2026-08-15", sent.Data["date"]);
        _mockRepo.Verify(r => r.MarkSentAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(notificationId)), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
