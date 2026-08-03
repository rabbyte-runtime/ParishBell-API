using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.Mass;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class MassReminderServiceTests
{
    private readonly Mock<IMassReminderRepository> _mockRepo;
    private readonly MassReminderService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _scheduleId = Guid.NewGuid();

    public MassReminderServiceTests()
    {
        _mockRepo = new Mock<IMassReminderRepository>();
        _service = new MassReminderService(_mockRepo.Object);
    }

    private void SetupSchedule(bool remindable) =>
        _mockRepo
            .Setup(r => r.IsScheduleRemindableAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(remindable);

    private void SetupUpsertReturns(MassReminderResult result) =>
        _mockRepo
            .Setup(r => r.UpsertAsync(_userId, _scheduleId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private SetMassReminderRequestDto Request(int minutesBefore = 30) =>
        new() { ScheduleId = _scheduleId, MinutesBefore = minutesBefore };

    private UserMassReminderResult MakeListResult(
        bool isActive = true,
        bool isFollowing = true,
        int dayOfWeek = 0,
        TimeOnly? massTime = null,
        bool isSpecial = false,
        DateOnly? validFrom = null,
        DateOnly? validTo = null) =>
        new(
            ReminderId: Guid.NewGuid(),
            MinutesBefore: 30,
            IsActive: isActive,
            ScheduleId: _scheduleId,
            LocationId: Guid.NewGuid(),
            LocationName: "St. Anthony's Shrine",
            DayOfWeek: dayOfWeek,
            MassTime: massTime ?? new TimeOnly(6, 30),
            Label: "Sinhala Mass",
            IsSpecial: isSpecial,
            ValidFrom: validFrom,
            ValidTo: validTo,
            IsFollowing: isFollowing);

    private void SetupListReturns(List<UserMassReminderResult> results) =>
        _mockRepo
            .Setup(r => r.GetForUserAsync(_userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

    // IMPORTANT: TEST 1 - A reminder on a live mass is saved and handed back for the bell to rebind
    [Fact]
    public async Task SetReminder_OnRemindableSchedule_ReturnsSavedReminder()
    {
        // NOTE: Arrange
        var reminderId = Guid.NewGuid();
        SetupSchedule(true);
        SetupUpsertReturns(new MassReminderResult(reminderId, 30, IsActive: true));

        // NOTE: Act
        var result = await _service.SetReminderAsync(_userId, Request(30));

        // NOTE: Assert
        Assert.Equal(reminderId, result.ReminderId);
        Assert.Equal(30, result.MinutesBefore);
        Assert.True(result.IsActive);
        _mockRepo.Verify(r => r.UpsertAsync(_userId, _scheduleId, 30, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 2 - A mass that is gone or hidden is a 404, and nothing is written
    [Fact]
    public async Task SetReminder_WhenScheduleNotRemindable_ThrowsNotFoundAndDoesNotWrite()
    {
        // NOTE: Arrange
        SetupSchedule(false);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.SetReminderAsync(_userId, Request()));
        Assert.Equal(MessageCodes.MassScheduleNotFound, exception.MessageCode);
        _mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 3 - The schedule is checked before the write, never after
    [Fact]
    public async Task SetReminder_ChecksScheduleBeforeWriting()
    {
        // NOTE: Arrange
        var callOrder = new List<string>();
        _mockRepo
            .Setup(r => r.IsScheduleRemindableAsync(_scheduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Callback(() => callOrder.Add("check"));
        _mockRepo
            .Setup(r => r.UpsertAsync(_userId, _scheduleId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MassReminderResult(Guid.NewGuid(), 15, IsActive: true))
            .Callback(() => callOrder.Add("upsert"));

        // NOTE: Act
        await _service.SetReminderAsync(_userId, Request(15));

        // NOTE: Assert
        Assert.Equal(["check", "upsert"], callOrder);
    }

    // IMPORTANT: TEST 4 - Re-saving reports it switched back on, which is how the UI re-enables
    [Fact]
    public async Task SetReminder_OnPreviouslyDisabledReminder_ComesBackActive()
    {
        // NOTE: Arrange — the repository re-enables on upsert
        var reminderId = Guid.NewGuid();
        SetupSchedule(true);
        SetupUpsertReturns(new MassReminderResult(reminderId, 60, IsActive: true));

        // NOTE: Act
        var result = await _service.SetReminderAsync(_userId, Request(60));

        // NOTE: Assert
        Assert.Equal(reminderId, result.ReminderId);
        Assert.Equal(60, result.MinutesBefore);
        Assert.True(result.IsActive);
    }

    // IMPORTANT: TEST 5 - The agenda row maps the reminder and the mass behind it, formatted for display
    [Fact]
    public async Task GetReminders_MapsReminderAndItsMass()
    {
        // NOTE: Arrange
        SetupListReturns([MakeListResult(dayOfWeek: 3, massTime: new TimeOnly(17, 5))]);

        // NOTE: Act
        var result = await _service.GetRemindersAsync(_userId, "en");

        // NOTE: Assert
        var dto = Assert.Single(result.Items);
        Assert.Equal(_scheduleId, dto.ScheduleId);
        Assert.Equal("St. Anthony's Shrine", dto.LocationName);
        Assert.Equal(3, dto.DayOfWeek);
        Assert.Equal("17:05", dto.MassTime);
        Assert.Equal("Sinhala Mass", dto.Label);
        Assert.Equal(30, dto.MinutesBefore);
        Assert.True(dto.IsActive);
        Assert.True(dto.IsFollowing);
        Assert.Null(dto.ValidFrom);
        Assert.Null(dto.ValidTo);
    }

    // IMPORTANT: TEST 6 - A cancelled reminder is listed rather than hidden, so it can be switched back on
    [Fact]
    public async Task GetReminders_IncludesCancelledOnesFlaggedOff()
    {
        // NOTE: Arrange
        SetupListReturns([MakeListResult(isActive: false)]);

        // NOTE: Act
        var result = await _service.GetRemindersAsync(_userId, "en");

        // NOTE: Assert
        var dto = Assert.Single(result.Items);
        Assert.False(dto.IsActive);
        Assert.Equal(_scheduleId, dto.ScheduleId);
    }

    // IMPORTANT: TEST 7 - A reminder at a church the user walked away from is surfaced, flagged, not dropped
    [Fact]
    public async Task GetReminders_FlagsRemindersAtUnfollowedChurches()
    {
        // NOTE: Arrange
        SetupListReturns([MakeListResult(isFollowing: false)]);

        // NOTE: Act
        var result = await _service.GetRemindersAsync(_userId, "en");

        // NOTE: Assert — it still fires, so hiding it would strand the user with a push they cannot find
        var dto = Assert.Single(result.Items);
        Assert.False(dto.IsFollowing);
        Assert.True(dto.IsActive);
    }

    // IMPORTANT: TEST 8 - A seasonal mass exposes its window, since this list has no month to clip against
    [Fact]
    public async Task GetReminders_SpecialMass_ExposesValidityWindow()
    {
        // NOTE: Arrange
        SetupListReturns([MakeListResult(
            isSpecial: true,
            validFrom: new DateOnly(2026, 12, 24),
            validTo: new DateOnly(2026, 12, 25))]);

        // NOTE: Act
        var result = await _service.GetRemindersAsync(_userId, "en");

        // NOTE: Assert
        var dto = Assert.Single(result.Items);
        Assert.True(dto.IsSpecial);
        Assert.Equal("2026-12-24", dto.ValidFrom);
        Assert.Equal("2026-12-25", dto.ValidTo);
    }

    // IMPORTANT: TEST 9 - Having set nothing is an empty list, not an error
    [Fact]
    public async Task GetReminders_WithNone_ReturnsEmptyList()
    {
        // NOTE: Arrange
        SetupListReturns([]);

        // NOTE: Act
        var result = await _service.GetRemindersAsync(_userId, "en");

        // NOTE: Assert
        Assert.Empty(result.Items);
    }

    // IMPORTANT: TEST 10 - The requested language reaches the repository, which resolves the fallback
    [Fact]
    public async Task GetReminders_PassesLanguageAndUserToRepository()
    {
        // NOTE: Arrange
        SetupListReturns([]);

        // NOTE: Act
        await _service.GetRemindersAsync(_userId, "ta");

        // NOTE: Assert
        _mockRepo.Verify(r => r.GetForUserAsync(_userId, "ta", It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 11 - Cancelling a reminder the caller owns switches it off
    [Fact]
    public async Task RemoveReminder_WhenOwned_Succeeds()
    {
        // NOTE: Arrange
        var reminderId = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.DisableAsync(_userId, reminderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // NOTE: Act
        await _service.RemoveReminderAsync(_userId, reminderId);

        // NOTE: Assert
        _mockRepo.Verify(r => r.DisableAsync(_userId, reminderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 12 - Someone else's reminder id is a 404, revealing nothing about its existence
    [Fact]
    public async Task RemoveReminder_WhenNotOwnedOrMissing_ThrowsNotFound()
    {
        // NOTE: Arrange
        _mockRepo
            .Setup(r => r.DisableAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.RemoveReminderAsync(_userId, Guid.NewGuid()));
        Assert.Equal(MessageCodes.GeneralNotFound, exception.MessageCode);
    }

    // IMPORTANT: TEST 13 - The reminder is always written for the caller
    [Fact]
    public async Task SetReminder_WritesForTheCallingUser()
    {
        // NOTE: Arrange
        SetupSchedule(true);
        SetupUpsertReturns(new MassReminderResult(Guid.NewGuid(), 45, IsActive: true));

        // NOTE: Act
        await _service.SetReminderAsync(_userId, Request(45));

        // NOTE: Assert
        _mockRepo.Verify(r => r.UpsertAsync(_userId, _scheduleId, 45, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.UpsertAsync(It.Is<Guid>(u => u != _userId), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
