using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class EventServiceTests
{
    private readonly Mock<IEventRepository> _mockRepo;
    private readonly EventService _service;

    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    public EventServiceTests()
    {
        _mockRepo = new Mock<IEventRepository>();
        _service = new EventService(_mockRepo.Object);
    }

    private EventDetailResult MakeResult(
        TimeOnly? startTime = null,
        TimeOnly? endTime = null,
        string? description = "Join us after the 6pm mass.",
        List<EventImageResult>? images = null) =>
        new(
            EventId: _eventId,
            LocationId: _locationId,
            LocationName: "St. Anthony's Shrine",
            EventDate: new DateOnly(2026, 8, 15),
            StartTime: startTime,
            EndTime: endTime,
            Title: "Feast of the Assumption",
            Description: description,
            Images: images ?? []);

    private void SetupRepoReturns(EventDetailResult? result) =>
        _mockRepo
            .Setup(r => r.GetEventByIdAsync(_eventId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    // IMPORTANT: TEST 1 - A deep-linked event maps every field the detail screen needs, church included
    [Fact]
    public async Task GetEventById_MapsAllFields()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult(startTime: new TimeOnly(18, 0), endTime: new TimeOnly(20, 30)));

        // NOTE: Act
        var dto = await _service.GetEventByIdAsync(_eventId, "en");

        // NOTE: Assert
        Assert.Equal(_eventId, dto.EventId);
        Assert.Equal(_locationId, dto.LocationId);
        Assert.Equal("St. Anthony's Shrine", dto.LocationName);
        Assert.Equal("2026-08-15", dto.EventDate);
        Assert.Equal("18:00", dto.StartTime);
        Assert.Equal("20:30", dto.EndTime);
        Assert.Equal("Feast of the Assumption", dto.Title);
        Assert.Equal("Join us after the 6pm mass.", dto.Description);
    }

    // IMPORTANT: TEST 2 - An all-day event has no times rather than midnight ones
    [Fact]
    public async Task GetEventById_AllDayEvent_LeavesTimesNull()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult(startTime: null, endTime: null, description: null));

        // NOTE: Act
        var dto = await _service.GetEventByIdAsync(_eventId, "en");

        // NOTE: Assert
        Assert.Null(dto.StartTime);
        Assert.Null(dto.EndTime);
        Assert.Null(dto.Description);
    }

    // IMPORTANT: TEST 3 - The gallery keeps the repository's sort order for the pager
    [Fact]
    public async Task GetEventById_MapsImagesInOrder()
    {
        // NOTE: Arrange
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        SetupRepoReturns(MakeResult(images: [
            new EventImageResult(first, "https://cdn/one.jpg", 0),
            new EventImageResult(second, "https://cdn/two.jpg", 1)
        ]));

        // NOTE: Act
        var dto = await _service.GetEventByIdAsync(_eventId, "en");

        // NOTE: Assert
        Assert.Equal([first, second], dto.Images.Select(i => i.EventImageId));
        Assert.Equal("https://cdn/one.jpg", dto.Images[0].ImageUrl);
        Assert.Equal([0, 1], dto.Images.Select(i => i.SortOrder));
    }

    // IMPORTANT: TEST 4 - An upcoming event simply has no photos yet, which is not an error
    [Fact]
    public async Task GetEventById_WithNoImages_ReturnsEmptyGallery()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult());

        // NOTE: Act
        var dto = await _service.GetEventByIdAsync(_eventId, "en");

        // NOTE: Assert
        Assert.Empty(dto.Images);
    }

    // IMPORTANT: TEST 5 - A link outliving its event is a 404, however the event became invisible
    [Fact]
    public async Task GetEventById_WhenMissingOrHidden_ThrowsNotFound()
    {
        // NOTE: Arrange — the repository applies the visibility rules and reports nothing found
        SetupRepoReturns(null);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetEventByIdAsync(_eventId, "en"));
        Assert.Equal(MessageCodes.EventNotFound, exception.MessageCode);
    }

    // IMPORTANT: TEST 6 - The requested language reaches the repository, which resolves the fallback
    [Fact]
    public async Task GetEventById_PassesLanguageToRepository()
    {
        // NOTE: Arrange
        SetupRepoReturns(MakeResult());

        // NOTE: Act
        await _service.GetEventByIdAsync(_eventId, "ta");

        // NOTE: Assert
        _mockRepo.Verify(r => r.GetEventByIdAsync(_eventId, "ta", It.IsAny<CancellationToken>()), Times.Once);
    }
}
