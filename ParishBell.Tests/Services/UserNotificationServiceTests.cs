using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class UserNotificationServiceTests
{
    private readonly Mock<IUserNotificationRepository> _mockRepo;
    private readonly UserNotificationService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    public UserNotificationServiceTests()
    {
        _mockRepo = new Mock<IUserNotificationRepository>();
        _service = new UserNotificationService(_mockRepo.Object);
    }

    private NotificationResult MakeResult(
        NotificationType type = NotificationType.Announcement,
        Guid? referenceId = null,
        Guid? locationId = null,
        Guid? calendarId = null,
        bool isRead = false,
        DateTime? sentAt = null) =>
        new()
        {
            NotificationId = Guid.NewGuid(),
            Type = (short)type,
            Title = "New announcement",
            Body = "Tap to listen",
            SentAt = sentAt ?? new DateTime(2026, 7, 29, 18, 45, 30, DateTimeKind.Utc),
            IsRead = isRead,
            ReferenceId = referenceId ?? Guid.NewGuid(),
            LocationId = locationId,
            CalendarId = calendarId
        };

    // NOTE: The repo returns whatever list the test provides, regardless of paging args.
    private void SetupRepoReturns(List<NotificationResult> results) =>
        _mockRepo
            .Setup(r => r.GetForUserAsync(_userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

    // IMPORTANT: TEST 1 - An announcement row maps every field, with an explicit-UTC timestamp
    [Fact]
    public async Task GetNotifications_MapsAllFields()
    {
        // NOTE: Arrange
        var announcementId = Guid.NewGuid();
        SetupRepoReturns([MakeResult(
            NotificationType.Announcement,
            referenceId: announcementId,
            locationId: _locationId,
            isRead: true)]);

        // NOTE: Act
        var page = await _service.GetNotificationsAsync(_userId, null, null);

        // NOTE: Assert
        var dto = Assert.Single(page.Items);
        Assert.Equal("Announcement", dto.Type);
        Assert.Equal("New announcement", dto.Title);
        Assert.Equal("Tap to listen", dto.Body);
        Assert.Equal("2026-07-29T18:45:30Z", dto.SentAt);
        Assert.True(dto.IsRead);
        Assert.Equal(_locationId, dto.LocationId);
        Assert.Equal(announcementId, dto.AnnouncementId);
        Assert.Null(dto.EventId);
        Assert.Null(dto.CalendarId);
    }

    // IMPORTANT: TEST 2 - The polymorphic reference lands in the typed id its own type deep-links on
    [Fact]
    public async Task GetNotifications_EventRow_PopulatesEventIdOnly()
    {
        // NOTE: Arrange
        var eventId = Guid.NewGuid();
        SetupRepoReturns([MakeResult(NotificationType.Event, referenceId: eventId, locationId: _locationId)]);

        // NOTE: Act
        var page = await _service.GetNotificationsAsync(_userId, null, null);

        // NOTE: Assert
        var dto = Assert.Single(page.Items);
        Assert.Equal("Event", dto.Type);
        Assert.Equal(eventId, dto.EventId);
        Assert.Equal(_locationId, dto.LocationId);
        Assert.Null(dto.AnnouncementId);
        Assert.Null(dto.CalendarId);
    }

    // IMPORTANT: TEST 3 - Feast days deep-link to the church and the calendar entry behind it
    [Fact]
    public async Task GetNotifications_FeastDayRow_PopulatesCalendarAndLocation()
    {
        // NOTE: Arrange — reference_id is the location_feast_days row, so the repo resolved both ids
        var calendarId = Guid.NewGuid();
        SetupRepoReturns([MakeResult(NotificationType.FeastDay, locationId: _locationId, calendarId: calendarId)]);

        // NOTE: Act
        var page = await _service.GetNotificationsAsync(_userId, null, null);

        // NOTE: Assert
        var dto = Assert.Single(page.Items);
        Assert.Equal("FeastDay", dto.Type);
        Assert.Equal(calendarId, dto.CalendarId);
        Assert.Equal(_locationId, dto.LocationId);
        Assert.Null(dto.EventId);
        Assert.Null(dto.AnnouncementId);
    }

    // IMPORTANT: TEST 4 - A mass reminder has no typed id of its own; the church is the tap target
    [Fact]
    public async Task GetNotifications_MassReminderRow_ExposesLocationOnly()
    {
        // NOTE: Arrange
        SetupRepoReturns([MakeResult(NotificationType.MassReminder, locationId: _locationId)]);

        // NOTE: Act
        var page = await _service.GetNotificationsAsync(_userId, null, null);

        // NOTE: Assert
        var dto = Assert.Single(page.Items);
        Assert.Equal("MassReminder", dto.Type);
        Assert.Equal(_locationId, dto.LocationId);
        Assert.Null(dto.EventId);
        Assert.Null(dto.AnnouncementId);
        Assert.Null(dto.CalendarId);
    }

    // IMPORTANT: TEST 5 - Default paging is page 1 / 20, and one extra row is fetched to probe for a next page
    [Fact]
    public async Task GetNotifications_WithoutPagingArgs_UsesDefaults()
    {
        // NOTE: Arrange
        SetupRepoReturns([MakeResult()]);

        // NOTE: Act
        var page = await _service.GetNotificationsAsync(_userId, null, null);

        // NOTE: Assert
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.False(page.HasMore);
        _mockRepo.Verify(r => r.GetForUserAsync(_userId, 0, 21, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 6 - The probe row is reported as hasMore and trimmed off the page
    [Fact]
    public async Task GetNotifications_WhenExtraRowComesBack_SetsHasMoreAndTrims()
    {
        // NOTE: Arrange — a full page of 3 plus the probe row
        SetupRepoReturns([MakeResult(), MakeResult(), MakeResult(), MakeResult()]);

        // NOTE: Act
        var page = await _service.GetNotificationsAsync(_userId, 1, 3);

        // NOTE: Assert
        Assert.True(page.HasMore);
        Assert.Equal(3, page.Items.Count);
    }

    // IMPORTANT: TEST 7 - Later pages skip the right number of rows
    [Fact]
    public async Task GetNotifications_LaterPage_SkipsPrecedingRows()
    {
        // NOTE: Arrange
        SetupRepoReturns([MakeResult()]);

        // NOTE: Act
        var page = await _service.GetNotificationsAsync(_userId, 3, 10);

        // NOTE: Assert — page 3 of 10 starts at row 20, and 11 are fetched
        Assert.Equal(3, page.Page);
        _mockRepo.Verify(r => r.GetForUserAsync(_userId, 20, 11, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 8 - An oversized pageSize is capped rather than honoured
    [Fact]
    public async Task GetNotifications_OversizedPageSize_IsCapped()
    {
        // NOTE: Arrange
        SetupRepoReturns([MakeResult()]);

        // NOTE: Act
        var page = await _service.GetNotificationsAsync(_userId, 1, 5000);

        // NOTE: Assert
        Assert.Equal(100, page.PageSize);
        _mockRepo.Verify(r => r.GetForUserAsync(_userId, 0, 101, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 9 - Nonsense paging values fall back to the defaults instead of a negative skip
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, -20)]
    public async Task GetNotifications_NonPositivePagingArgs_FallBackToDefaults(int page, int pageSize)
    {
        // NOTE: Arrange
        SetupRepoReturns([MakeResult()]);

        // NOTE: Act
        var result = await _service.GetNotificationsAsync(_userId, page, pageSize);

        // NOTE: Assert
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        _mockRepo.Verify(r => r.GetForUserAsync(_userId, 0, 21, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 10 - An empty inbox is an empty page, not an error
    [Fact]
    public async Task GetNotifications_WithNoRows_ReturnsEmptyPage()
    {
        // NOTE: Arrange
        SetupRepoReturns([]);

        // NOTE: Act
        var page = await _service.GetNotificationsAsync(_userId, null, null);

        // NOTE: Assert
        Assert.Empty(page.Items);
        Assert.False(page.HasMore);
    }

    // IMPORTANT: TEST 11 - Marking read is scoped to the caller
    [Fact]
    public async Task MarkRead_WhenOwned_Succeeds()
    {
        // NOTE: Arrange
        var notificationId = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.MarkReadAsync(_userId, notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // NOTE: Act
        await _service.MarkReadAsync(_userId, notificationId);

        // NOTE: Assert
        _mockRepo.Verify(r => r.MarkReadAsync(_userId, notificationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 12 - Someone else's notification id is a 404, revealing nothing about its existence
    [Fact]
    public async Task MarkRead_WhenNotOwnedOrMissing_ThrowsNotFound()
    {
        // NOTE: Arrange
        _mockRepo
            .Setup(r => r.MarkReadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkReadAsync(_userId, Guid.NewGuid()));
        Assert.Equal(MessageCodes.GeneralNotFound, exception.MessageCode);
    }

    // IMPORTANT: TEST 13 - Clearing the badge forwards to the repo for this user only
    [Fact]
    public async Task MarkAllRead_ForwardsToRepositoryScopedToUser()
    {
        // NOTE: Arrange
        _mockRepo
            .Setup(r => r.MarkAllReadAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        // NOTE: Act — an already-read inbox (0 rows) must not throw either
        await _service.MarkAllReadAsync(_userId);

        // NOTE: Assert
        _mockRepo.Verify(r => r.MarkAllReadAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
