using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class LiturgicalCalendarServiceTests
{
    private readonly Mock<ILiturgicalCalendarRepository> _mockRepo;
    private readonly LiturgicalCalendarService _service;

    public LiturgicalCalendarServiceTests()
    {
        // NOTE: The service only depends on the repository — mock it
        _mockRepo = new Mock<ILiturgicalCalendarRepository>();
        _service = new LiturgicalCalendarService(_mockRepo.Object);
    }

    // NOTE: Helper to stub the repository with a fixed set of results
    private void SetupRepo(params LiturgicalCalendarResult[] results) =>
        _mockRepo
            .Setup(r => r.GetByMonthYearAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results.ToList());

    // IMPORTANT: TEST 1 - Recurring annual entry resolves its date against the requested year
    [Fact]
    public async Task GetByMonthYearAsync_RecurringEntry_ResolvesDateForRequestedYear()
    {
        // NOTE: Arrange - a fixed annual feast on July 25 (month+day, no specific date)
        SetupRepo(new LiturgicalCalendarResult(
            Guid.NewGuid(), Month: 7, Day: 25, SpecificDate: null,
            IsRecurringAnnually: true, IsHolyDay: false, Title: "Feast of St. James", Description: null));

        // NOTE: Act
        var result = await _service.GetByMonthYearAsync(7, 2026, "en");

        // NOTE: Assert - date is pinned to the requested year; specificDate stays null
        var item = Assert.Single(result.Items);
        Assert.Equal("2026-07-25", item.Date);
        Assert.Null(item.SpecificDate);
        Assert.Equal(7, item.Month);
        Assert.Equal(25, item.Day);
        Assert.True(item.IsRecurringAnnually);
    }

    // IMPORTANT: TEST 2 - One-off entry uses its specific date verbatim
    [Fact]
    public async Task GetByMonthYearAsync_OneOffEntry_UsesSpecificDate()
    {
        // NOTE: Arrange - a one-off entry pinned to an exact date
        SetupRepo(new LiturgicalCalendarResult(
            Guid.NewGuid(), Month: null, Day: null, SpecificDate: new DateOnly(2026, 7, 10),
            IsRecurringAnnually: false, IsHolyDay: true, Title: "Diocesan Jubilee", Description: "One-off celebration"));

        // NOTE: Act
        var result = await _service.GetByMonthYearAsync(7, 2026, "en");

        // NOTE: Assert - both the resolved date and specificDate reflect the exact date
        var item = Assert.Single(result.Items);
        Assert.Equal("2026-07-10", item.Date);
        Assert.Equal("2026-07-10", item.SpecificDate);
        Assert.False(item.IsRecurringAnnually);
        Assert.True(item.IsHolyDay);
    }

    // IMPORTANT: TEST 3 - Recurring Feb 29 in a non-leap year yields a null date but still appears
    [Fact]
    public async Task GetByMonthYearAsync_RecurringFeb29_NonLeapYear_ReturnsNullDateButKeepsEntry()
    {
        // NOTE: Arrange - a recurring entry on Feb 29
        SetupRepo(new LiturgicalCalendarResult(
            Guid.NewGuid(), Month: 2, Day: 29, SpecificDate: null,
            IsRecurringAnnually: true, IsHolyDay: false, Title: "Leap Day Observance", Description: null));

        // NOTE: Act - 2025 is not a leap year
        var result = await _service.GetByMonthYearAsync(2, 2025, "en");

        // NOTE: Assert - the date can't be resolved, but the entry (and its month/day) is preserved
        var item = Assert.Single(result.Items);
        Assert.Null(item.Date);
        Assert.Equal(2, item.Month);
        Assert.Equal(29, item.Day);
    }

    // IMPORTANT: TEST 4 - Recurring Feb 29 resolves correctly in a leap year
    [Fact]
    public async Task GetByMonthYearAsync_RecurringFeb29_LeapYear_ResolvesDate()
    {
        // NOTE: Arrange
        SetupRepo(new LiturgicalCalendarResult(
            Guid.NewGuid(), Month: 2, Day: 29, SpecificDate: null,
            IsRecurringAnnually: true, IsHolyDay: false, Title: "Leap Day Observance", Description: null));

        // NOTE: Act - 2024 is a leap year
        var result = await _service.GetByMonthYearAsync(2, 2024, "en");

        // NOTE: Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("2024-02-29", item.Date);
    }

    // IMPORTANT: TEST 5 - Empty repository result returns an empty list but still echoes the filter
    [Fact]
    public async Task GetByMonthYearAsync_NoEntries_ReturnsEmptyListWithEchoedFilter()
    {
        // NOTE: Arrange - repository finds nothing
        SetupRepo();

        // NOTE: Act
        var result = await _service.GetByMonthYearAsync(3, 2030, "en");

        // NOTE: Assert - the response still reports which month/year was queried
        Assert.Empty(result.Items);
        Assert.Equal(3, result.Month);
        Assert.Equal(2030, result.Year);
    }

    // IMPORTANT: TEST 6 - The requested month/year/language are forwarded to the repository unchanged
    [Fact]
    public async Task GetByMonthYearAsync_ForwardsFiltersToRepository()
    {
        // NOTE: Arrange
        SetupRepo();

        // NOTE: Act
        await _service.GetByMonthYearAsync(11, 2027, "si");

        // NOTE: Assert - exact passthrough, called once
        _mockRepo.Verify(r => r.GetByMonthYearAsync(11, 2027, "si", It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 7 - All entry fields are mapped from the repository result
    [Fact]
    public async Task GetByMonthYearAsync_MapsAllEntryFields()
    {
        // NOTE: Arrange
        var calendarId = Guid.NewGuid();
        SetupRepo(new LiturgicalCalendarResult(
            calendarId, Month: 12, Day: 25, SpecificDate: null,
            IsRecurringAnnually: true, IsHolyDay: true, Title: "Christmas", Description: "The Nativity of the Lord"));

        // NOTE: Act
        var result = await _service.GetByMonthYearAsync(12, 2026, "en");

        // NOTE: Assert
        var item = Assert.Single(result.Items);
        Assert.Equal(calendarId, item.CalendarId);
        Assert.Equal("Christmas", item.Title);
        Assert.Equal("The Nativity of the Lord", item.Description);
        Assert.True(item.IsHolyDay);
        Assert.True(item.IsRecurringAnnually);
        Assert.Equal("2026-12-25", item.Date);
    }
}
