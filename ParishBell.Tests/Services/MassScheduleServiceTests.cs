using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class MassScheduleServiceTests
{
    private readonly Mock<IMassScheduleRepository> _mockRepo;
    private readonly MassScheduleService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    // NOTE: August 2026 starts on a Saturday - 5 Saturdays and Sundays, 4 of the rest.
    private const int Month = 8;
    private const int Year = 2026;

    public MassScheduleServiceTests()
    {
        _mockRepo = new Mock<IMassScheduleRepository>();
        _service = new MassScheduleService(_mockRepo.Object);
    }

    private MassSchedulePatternResult MakeResult(
        int dayOfWeek = 0,
        TimeOnly? massTime = null,
        bool isSpecial = false,
        DateOnly? validFrom = null,
        DateOnly? validTo = null,
        MassReminderResult? reminder = null) =>
        new(
            ScheduleId: Guid.NewGuid(),
            LocationId: _locationId,
            LocationName: "St. Anthony's Shrine",
            DayOfWeek: dayOfWeek,
            MassTime: massTime ?? new TimeOnly(6, 30),
            Label: "Sinhala Mass",
            IsSpecial: isSpecial,
            ValidFrom: validFrom,
            ValidTo: validTo,
            Reminder: reminder);

    private void SetupRepoReturns(List<MassSchedulePatternResult> results) =>
        _mockRepo
            .Setup(r => r.GetForFollowedLocationsAsync(
                _userId, It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

    // IMPORTANT: TEST 1 - A weekly mass expands onto every matching weekday in the month
    [Fact]
    public async Task GetFollowedMassSchedules_WeeklyEntry_ExpandsOntoEveryMatchingDay()
    {
        // NOTE: Arrange — Sunday, and August 2026 has five of them
        SetupRepoReturns([MakeResult(dayOfWeek: 0, massTime: new TimeOnly(6, 30))]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert
        Assert.Equal(
            ["2026-08-02", "2026-08-09", "2026-08-16", "2026-08-23", "2026-08-30"],
            result.Items.Select(i => i.Date));
        Assert.All(result.Items, i => Assert.Equal("06:30", i.MassTime));
    }

    // IMPORTANT: TEST 2 - A month starting on the schedule's own weekday includes day 1
    [Fact]
    public async Task GetFollowedMassSchedules_WhenMonthStartsOnThatWeekday_IncludesTheFirst()
    {
        // NOTE: Arrange — 1 Aug 2026 is a Saturday
        SetupRepoReturns([MakeResult(dayOfWeek: 6)]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert
        Assert.Equal("2026-08-01", result.Items.First().Date);
        Assert.Equal(5, result.Items.Count);
    }

    // IMPORTANT: TEST 3 - A special mass is clipped to its own window, not the whole month
    [Fact]
    public async Task GetFollowedMassSchedules_SpecialMass_ClippedToItsWindow()
    {
        // NOTE: Arrange — Sundays, but only valid across the middle of the month
        SetupRepoReturns([MakeResult(
            dayOfWeek: 0,
            isSpecial: true,
            validFrom: new DateOnly(2026, 8, 10),
            validTo: new DateOnly(2026, 8, 24))]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert — the 2nd and 30th fall outside the window
        Assert.Equal(["2026-08-16", "2026-08-23"], result.Items.Select(i => i.Date));
        Assert.All(result.Items, i => Assert.True(i.IsSpecial));
    }

    // IMPORTANT: TEST 4 - A window that misses every matching weekday yields nothing rather than a stray row
    [Fact]
    public async Task GetFollowedMassSchedules_SpecialMassWindowMissingItsWeekday_YieldsNoOccurrence()
    {
        // NOTE: Arrange — Sundays, with a window covering only Mon 17th to Fri 21st
        SetupRepoReturns([MakeResult(
            dayOfWeek: 0,
            isSpecial: true,
            validFrom: new DateOnly(2026, 8, 17),
            validTo: new DateOnly(2026, 8, 21))]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert
        Assert.Empty(result.Items);
    }

    // IMPORTANT: TEST 5 - An open-ended window means the month edge, not an empty one
    [Fact]
    public async Task GetFollowedMassSchedules_SpecialMassWithOpenEndedWindow_RunsToTheMonthEdge()
    {
        // NOTE: Arrange — starts mid-month, never closes
        SetupRepoReturns([MakeResult(
            dayOfWeek: 0,
            isSpecial: true,
            validFrom: new DateOnly(2026, 8, 10),
            validTo: null)]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert
        Assert.Equal(["2026-08-16", "2026-08-23", "2026-08-30"], result.Items.Select(i => i.Date));
    }

    // IMPORTANT: TEST 6 - A weekly entry ignores any window left on the row; only specials are seasonal
    [Fact]
    public async Task GetFollowedMassSchedules_WeeklyEntryWithStrayWindow_IgnoresIt()
    {
        // NOTE: Arrange — not special, so the dates hanging off the row must not clip it
        SetupRepoReturns([MakeResult(
            dayOfWeek: 0,
            isSpecial: false,
            validFrom: new DateOnly(2026, 8, 10),
            validTo: new DateOnly(2026, 8, 12))]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert
        Assert.Equal(5, result.Items.Count);
    }

    // IMPORTANT: TEST 7 - The reminder rides on every occurrence of its schedule
    [Fact]
    public async Task GetFollowedMassSchedules_WithReminder_RepeatsItOnEveryOccurrence()
    {
        // NOTE: Arrange
        var reminderId = Guid.NewGuid();
        SetupRepoReturns([MakeResult(dayOfWeek: 0, reminder: new MassReminderResult(reminderId, 30, IsActive: true))]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert
        Assert.Equal(5, result.Items.Count);
        Assert.All(result.Items, i =>
        {
            Assert.NotNull(i.Reminder);
            Assert.Equal(reminderId, i.Reminder.ReminderId);
            Assert.Equal(30, i.Reminder.MinutesBefore);
            Assert.True(i.Reminder.IsActive);
        });
    }

    // IMPORTANT: TEST 8 - A switched-off reminder is still returned, rendering off
    [Fact]
    public async Task GetFollowedMassSchedules_WithInactiveReminder_KeepsItButFlagsItOff()
    {
        // NOTE: Arrange
        SetupRepoReturns([MakeResult(reminder: new MassReminderResult(Guid.NewGuid(), 15, IsActive: false))]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert
        Assert.All(result.Items, i =>
        {
            Assert.NotNull(i.Reminder);
            Assert.False(i.Reminder.IsActive);
        });
    }

    // IMPORTANT: TEST 9 - Expansion interleaves schedules, so the flat list is re-sorted by date then time
    [Fact]
    public async Task GetFollowedMassSchedules_OrdersByDateThenTime()
    {
        // NOTE: Arrange — a late Saturday mass and an early Sunday one, returned in day-of-week order
        SetupRepoReturns([
            MakeResult(dayOfWeek: 0, massTime: new TimeOnly(6, 30)),
            MakeResult(dayOfWeek: 6, massTime: new TimeOnly(18, 0))
        ]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert — Sat 1st comes before Sun 2nd, despite Sunday sorting first by weekday
        Assert.Equal(("2026-08-01", "18:00"), (result.Items[0].Date, result.Items[0].MassTime));
        Assert.Equal(("2026-08-02", "06:30"), (result.Items[1].Date, result.Items[1].MassTime));
    }

    // IMPORTANT: TEST 10 - Every occurrence carries its church, so the merged calendar can label the row
    [Fact]
    public async Task GetFollowedMassSchedules_TagsEveryOccurrenceWithItsChurch()
    {
        // NOTE: Arrange
        SetupRepoReturns([MakeResult(dayOfWeek: 0)]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert
        Assert.All(result.Items, i =>
        {
            Assert.Equal(_locationId, i.LocationId);
            Assert.Equal("St. Anthony's Shrine", i.LocationName);
            Assert.Equal("Sinhala Mass", i.Label);
        });
    }

    // IMPORTANT: TEST 11 - The month is bounded to its first and last day before it reaches the repository
    [Fact]
    public async Task GetFollowedMassSchedules_PassesMonthBoundsAndLanguageToRepository()
    {
        // NOTE: Arrange — February 2028 is a leap month, so the last day must be the 29th
        SetupRepoReturns([]);

        // NOTE: Act
        await _service.GetFollowedMassSchedulesAsync(_userId, "si", 2, 2028);

        // NOTE: Assert
        _mockRepo.Verify(r => r.GetForFollowedLocationsAsync(
            _userId,
            "si",
            new DateOnly(2028, 2, 1),
            new DateOnly(2028, 2, 29),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 12 - Following nothing is an empty month with the window echoed
    [Fact]
    public async Task GetFollowedMassSchedules_WithNoFollowedLocations_ReturnsEmptyMonth()
    {
        // NOTE: Arrange
        SetupRepoReturns([]);

        // NOTE: Act
        var result = await _service.GetFollowedMassSchedulesAsync(_userId, "en", Month, Year);

        // NOTE: Assert
        Assert.Empty(result.Items);
        Assert.Equal(Month, result.Month);
        Assert.Equal(Year, result.Year);
    }
}
