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

    // IMPORTANT: TEST 4 - Re-saving reports the reminder switched back on, which is how the UI re-enables one
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

    // IMPORTANT: TEST 5 - The reminder is always written for the caller, never for whoever the body might name
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
