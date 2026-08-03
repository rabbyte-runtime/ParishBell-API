using Moq;
using ParishBell.Application.Services;
using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Tests.Services;

public class AnnouncementServiceTests
{
    private readonly Mock<IAnnouncementRepository> _mockRepo;
    private readonly Mock<ILocationFollowRepository> _mockFollowRepo;
    private readonly Mock<IBlobUrlSigner> _mockUrlSigner;
    private readonly AnnouncementService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    public AnnouncementServiceTests()
    {
        _mockRepo = new Mock<IAnnouncementRepository>();
        _mockFollowRepo = new Mock<ILocationFollowRepository>();

        // NOTE: Re-signing is exercised in its own tests; here it hands the URL straight back so assertions stay readable.
        _mockUrlSigner = new Mock<IBlobUrlSigner>();
        _mockUrlSigner
            .Setup(s => s.ResignAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? url, CancellationToken _) => url);

        _service = new AnnouncementService(_mockRepo.Object, _mockFollowRepo.Object, _mockUrlSigner.Object);
    }

    // NOTE: The caller follows the location unless a test says otherwise.
    private void SetupFollowing(bool following = true) =>
        _mockFollowRepo
            .Setup(r => r.IsFollowingAsync(_userId, _locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(following);

    // NOTE: Repo returns whatever list the test provides, regardless of paging args.
    private void SetupRepoReturns(List<AnnouncementResult> results) =>
        _mockRepo
            .Setup(r => r.GetLocationAnnouncementsAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

    private static AnnouncementResult MakeResult(
        short mediaType = 1,
        string? thumbnailUrl = "https://blob/thumb.jpg",
        int durationSeconds = 45,
        string title = "Title",
        string? description = "Description",
        DateTime? createdAt = null,
        DateTime? expiresAt = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            mediaType,
            "https://blob/media?sas",
            thumbnailUrl,
            durationSeconds,
            title,
            description,
            createdAt ?? new DateTime(2026, 7, 12, 9, 30, 0, DateTimeKind.Utc),
            expiresAt ?? new DateTime(2026, 7, 13, 9, 30, 0, DateTimeKind.Utc));

    // IMPORTANT: TEST 1 - Non-follower is forbidden and the channel is never queried
    [Fact]
    public async Task GetLocationAnnouncements_WhenNotFollowing_ThrowsForbidden()
    {
        // NOTE: Arrange
        SetupFollowing(false);

        // NOTE: Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", null, null));
        Assert.Equal(MessageCodes.LocationAnnouncementsForbidden, exception.MessageCode);

        // IMPORTANT: The follower gate runs before any data access
        _mockRepo.Verify(r => r.GetLocationAnnouncementsAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(),
            It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // IMPORTANT: TEST 2 - Follower gets each field mapped correctly
    [Fact]
    public async Task GetLocationAnnouncements_WhenFollowing_MapsAllFields()
    {
        // NOTE: Arrange
        SetupFollowing();
        var announcementId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var result = new AnnouncementResult(
            announcementId,
            locationId,
            2,                                   // NOTE: Video
            "https://blob/media?sas",
            "https://blob/thumb.jpg",
            120,
            "Sunday Mass",
            "Join us this Sunday",
            new DateTime(2026, 7, 12, 9, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 13, 9, 30, 0, DateTimeKind.Utc));
        SetupRepoReturns([result]);

        // NOTE: Act
        var page = await _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", null, null);

        // NOTE: Assert
        var dto = Assert.Single(page.Items);
        Assert.Equal(announcementId, dto.AnnouncementId);
        Assert.Equal(locationId, dto.LocationId);
        Assert.Equal("Video", dto.MediaType);
        Assert.Equal("https://blob/media?sas", dto.MediaUrl);
        Assert.Equal("https://blob/thumb.jpg", dto.ThumbnailUrl);
        Assert.Equal(120, dto.DurationSeconds);
        Assert.Equal("Sunday Mass", dto.Title);
        Assert.Equal("Join us this Sunday", dto.Description);
        Assert.Equal("2026-07-12T09:30:00Z", dto.CreatedAt);
        Assert.Equal("2026-07-13T09:30:00Z", dto.ExpiresAt);
    }

    // IMPORTANT: TEST 3 - Numeric media type maps to the client-facing enum name
    [Theory]
    [InlineData((short)1, "Audio")]
    [InlineData((short)2, "Video")]
    public async Task GetLocationAnnouncements_MapsMediaType(short raw, string expected)
    {
        // NOTE: Arrange
        SetupFollowing();
        SetupRepoReturns([MakeResult(mediaType: raw)]);

        // NOTE: Act
        var page = await _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", null, null);

        // NOTE: Assert
        Assert.Equal(expected, Assert.Single(page.Items).MediaType);
    }

    // IMPORTANT: TEST 4 - Nullable fields pass through as null
    [Fact]
    public async Task GetLocationAnnouncements_NullThumbnailAndDescription_PassThrough()
    {
        // NOTE: Arrange
        SetupFollowing();
        SetupRepoReturns([MakeResult(thumbnailUrl: null, description: null)]);

        // NOTE: Act
        var page = await _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", null, null);

        // NOTE: Assert
        var dto = Assert.Single(page.Items);
        Assert.Null(dto.ThumbnailUrl);
        Assert.Null(dto.Description);
    }

    // IMPORTANT: TEST 5 - Timestamps of unspecified kind are still emitted as UTC "Z"
    [Fact]
    public async Task GetLocationAnnouncements_FormatsTimestampsAsUtc()
    {
        // NOTE: Arrange — DB reads may arrive with Kind=Unspecified; they are UTC by contract
        SetupFollowing();
        var created = new DateTime(2026, 1, 5, 6, 7, 8, DateTimeKind.Unspecified);
        var expires = new DateTime(2026, 1, 6, 6, 7, 8, DateTimeKind.Unspecified);
        SetupRepoReturns([MakeResult(createdAt: created, expiresAt: expires)]);

        // NOTE: Act
        var page = await _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", null, null);

        // NOTE: Assert — no local-time shift, explicit trailing Z
        var dto = Assert.Single(page.Items);
        Assert.Equal("2026-01-05T06:07:08Z", dto.CreatedAt);
        Assert.Equal("2026-01-06T06:07:08Z", dto.ExpiresAt);
    }

    // IMPORTANT: TEST 6 - No paging params returns the full list and asks the repo for no window
    [Fact]
    public async Task GetLocationAnnouncements_WithoutPaging_ReturnsAllAndPassesNullSkipTake()
    {
        // NOTE: Arrange
        SetupFollowing();
        SetupRepoReturns([MakeResult(), MakeResult(), MakeResult(), MakeResult(), MakeResult()]);

        // NOTE: Act
        var page = await _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", null, null);

        // NOTE: Assert
        Assert.Equal(5, page.Items.Count);
        Assert.False(page.HasMore);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);

        // IMPORTANT: Unpaged requests must not skip/take at the DB level
        _mockRepo.Verify(r => r.GetLocationAnnouncementsAsync(
            _locationId, "en", It.IsAny<DateTime>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 7 - Paged request fetches pageSize+1 and reports hasMore when the extra row exists
    [Fact]
    public async Task GetLocationAnnouncements_FirstPage_FetchesOneExtra_TrimsAndSetsHasMore()
    {
        // NOTE: Arrange — request 2 per page; repo yields the sentinel extra row (3)
        SetupFollowing();
        SetupRepoReturns([MakeResult(), MakeResult(), MakeResult()]);

        // NOTE: Act
        var page = await _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", 1, 2);

        // NOTE: Assert
        Assert.Equal(2, page.Items.Count);   // NOTE: trimmed back to pageSize
        Assert.True(page.HasMore);
        Assert.Equal(1, page.Page);
        Assert.Equal(2, page.PageSize);

        // IMPORTANT: skip=0, take=pageSize+1
        _mockRepo.Verify(r => r.GetLocationAnnouncementsAsync(
            _locationId, "en", It.IsAny<DateTime>(), 0, 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 8 - Second page computes the correct skip offset
    [Fact]
    public async Task GetLocationAnnouncements_SecondPage_ComputesSkip()
    {
        // NOTE: Arrange — page 2 of size 2, no extra row returned
        SetupFollowing();
        SetupRepoReturns([MakeResult(), MakeResult()]);

        // NOTE: Act
        var page = await _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", 2, 2);

        // NOTE: Assert
        Assert.Equal(2, page.Items.Count);
        Assert.False(page.HasMore);          // NOTE: exactly pageSize, no sentinel
        Assert.Equal(2, page.Page);

        // IMPORTANT: skip=(page-1)*pageSize=2, take=pageSize+1=3
        _mockRepo.Verify(r => r.GetLocationAnnouncementsAsync(
            _locationId, "en", It.IsAny<DateTime>(), 2, 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 9 - Exactly pageSize rows means there is no next page
    [Fact]
    public async Task GetLocationAnnouncements_ExactlyPageSize_HasMoreFalse()
    {
        // NOTE: Arrange
        SetupFollowing();
        SetupRepoReturns([MakeResult(), MakeResult()]);

        // NOTE: Act
        var page = await _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", 1, 2);

        // NOTE: Assert
        Assert.Equal(2, page.Items.Count);
        Assert.False(page.HasMore);
    }

    // IMPORTANT: TEST 10 - Missing pageSize falls back to 20 while still paging
    [Fact]
    public async Task GetLocationAnnouncements_PageWithoutPageSize_DefaultsTo20()
    {
        // NOTE: Arrange
        SetupFollowing();
        SetupRepoReturns([MakeResult()]);

        // NOTE: Act
        var page = await _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", 1, null);

        // NOTE: Assert
        Assert.Equal(20, page.PageSize);

        // IMPORTANT: take=default(20)+1=21
        _mockRepo.Verify(r => r.GetLocationAnnouncementsAsync(
            _locationId, "en", It.IsAny<DateTime>(), 0, 21, It.IsAny<CancellationToken>()), Times.Once);
    }

    // IMPORTANT: TEST 11 - Empty channel returns an empty page, not null
    [Fact]
    public async Task GetLocationAnnouncements_WhenEmpty_ReturnsEmptyPage()
    {
        // NOTE: Arrange
        SetupFollowing();
        SetupRepoReturns([]);

        // NOTE: Act
        var page = await _service.GetLocationAnnouncementsAsync(_userId, _locationId, "en", 1, 20);

        // NOTE: Assert
        Assert.Empty(page.Items);
        Assert.False(page.HasMore);
    }
}
