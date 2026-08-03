using ParishBell.Application.Services;
using ParishBell.Core.DTOs.Common;

namespace ParishBell.Tests.Services;

// NOTE: The expansion the calendar and the church profile now share.
// NOTE: The rules live here rather than being asserted twice through their services.
public class MassOccurrenceExpanderTests
{
    private readonly Guid _locationId = Guid.NewGuid();

    private MassSchedulePatternResult MakePattern(
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

    // IMPORTANT: TEST 1 - A one-week window yields exactly one occurrence of a weekly mass
    [Fact]
    public void Expand_WeeklyPatternOverAWeek_YieldsOneOccurrence()
    {
        // NOTE: Arrange — Sunday mass, window Sat 1 Aug to Fri 7 Aug 2026
        var items = MassOccurrenceExpander.Expand(
            [MakePattern(dayOfWeek: 0)],
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 7));

        // NOTE: Assert
        var item = Assert.Single(items);
        Assert.Equal("2026-08-02", item.Date);
        Assert.Equal("06:30", item.MassTime);
        Assert.Equal(_locationId, item.LocationId);
    }

    // IMPORTANT: TEST 2 - The same pattern over a month yields every matching weekday
    [Fact]
    public void Expand_WeeklyPatternOverAMonth_YieldsEveryMatchingDay()
    {
        // NOTE: Arrange
        var items = MassOccurrenceExpander.Expand(
            [MakePattern(dayOfWeek: 0)],
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // NOTE: Assert
        Assert.Equal(
            ["2026-08-02", "2026-08-09", "2026-08-16", "2026-08-23", "2026-08-30"],
            items.Select(i => i.Date));
    }

    // IMPORTANT: TEST 3 - A special is clipped to its own window, whatever window was asked for
    [Fact]
    public void Expand_SpecialPattern_ClippedToItsWindow()
    {
        // NOTE: Arrange
        var items = MassOccurrenceExpander.Expand(
            [MakePattern(dayOfWeek: 0, isSpecial: true, validFrom: new DateOnly(2026, 8, 10), validTo: new DateOnly(2026, 8, 24))],
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // NOTE: Assert
        Assert.Equal(["2026-08-16", "2026-08-23"], items.Select(i => i.Date));
    }

    // IMPORTANT: TEST 4 - A window containing no matching weekday yields nothing rather than a stray row
    [Fact]
    public void Expand_WindowWithoutThatWeekday_YieldsNothing()
    {
        // NOTE: Arrange — Sunday mass, window Mon 3 Aug to Fri 7 Aug
        var items = MassOccurrenceExpander.Expand(
            [MakePattern(dayOfWeek: 0)],
            new DateOnly(2026, 8, 3),
            new DateOnly(2026, 8, 7));

        // NOTE: Assert
        Assert.Empty(items);
    }

    // IMPORTANT: TEST 5 - Occurrences interleave across schedules and come back in date-then-time order
    [Fact]
    public void Expand_MultiplePatterns_OrdersByDateThenTime()
    {
        // NOTE: Arrange — a Sunday 06:30 and a Saturday 18:00, given in weekday order
        var items = MassOccurrenceExpander.Expand(
            [MakePattern(dayOfWeek: 0, massTime: new TimeOnly(6, 30)), MakePattern(dayOfWeek: 6, massTime: new TimeOnly(18, 0))],
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 7));

        // NOTE: Assert — Sat 1st precedes Sun 2nd, despite Sunday sorting first by weekday
        Assert.Equal(("2026-08-01", "18:00"), (items[0].Date, items[0].MassTime));
        Assert.Equal(("2026-08-02", "06:30"), (items[1].Date, items[1].MassTime));
    }

    // IMPORTANT: TEST 6 - The reminder repeats on every occurrence of its schedule
    [Fact]
    public void Expand_WithReminder_RepeatsItOnEveryOccurrence()
    {
        // NOTE: Arrange
        var reminderId = Guid.NewGuid();
        var items = MassOccurrenceExpander.Expand(
            [MakePattern(dayOfWeek: 0, reminder: new MassReminderResult(reminderId, 30, IsActive: true))],
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // NOTE: Assert
        Assert.Equal(5, items.Count);
        Assert.All(items, i =>
        {
            Assert.NotNull(i.Reminder);
            Assert.Equal(reminderId, i.Reminder.ReminderId);
        });
    }

    // IMPORTANT: TEST 7 - Without a reminder the bell is simply absent, not a zeroed object
    [Fact]
    public void Expand_WithoutReminder_LeavesItNull()
    {
        // NOTE: Arrange
        var items = MassOccurrenceExpander.Expand(
            [MakePattern(dayOfWeek: 0)],
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 7));

        // NOTE: Assert
        Assert.Null(Assert.Single(items).Reminder);
    }
}
