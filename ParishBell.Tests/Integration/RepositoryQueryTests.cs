using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Infrastructure.Data;
using ParishBell.Infrastructure.Repositories;

namespace ParishBell.Tests.Integration;

// NOTE: One seeded world shared by every test here.
// NOTE: A church with a Sunday mass, a follower with a reminder, and one of each notification.
[Collection(nameof(PostgresCollection))]
public class RepositoryQueryTests(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly PostgresFixture _fixture = fixture;

    private readonly Guid _englishId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _locationTypeId = Guid.NewGuid();
    private readonly Guid _scheduleId = Guid.NewGuid();
    private readonly Guid _calendarId = Guid.NewGuid();
    private readonly Guid _feastDayId = Guid.NewGuid();
    private readonly Guid _reminderId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        using var db = _fixture.CreateContext();

        // NOTE: Cleared first so a rerun against the same container starts from the same state.
        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE notifications_log, user_mass_reminders, user_followed_locations, mass_schedule_translations,
                     mass_schedules, location_feast_days, liturgical_calendar, announcements, admin_users,
                     location_translations, locations, location_types, app_users, languages RESTART IDENTITY CASCADE;
            """);

        db.Languages.Add(new Language
        {
            LanguageId = _englishId,
            LanguageCode = "en",
            LanguageName = "English",
            NativeName = "English",
            IsActive = true
        });

        db.LocationTypes.Add(new LocationType
        {
            LocationTypeId = _locationTypeId,
            LocationTypeCode = "SHRINE",
            SortOrder = 1,
            PinColorHex = "#B23A48",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        db.Locations.Add(new Location
        {
            LocationId = _locationId,
            LocationTypeId = _locationTypeId,
            Latitude = 6.9271m,
            Longitude = 79.8612m,
            IsApproved = true,
            IsActive = true,
            IsRejected = false,
            CreatedAt = DateTime.UtcNow
        });

        db.LocationTranslations.Add(new LocationTranslation
        {
            TranslationId = Guid.NewGuid(),
            LocationId = _locationId,
            LanguageId = _englishId,
            Name = "St. Anthony's Shrine",
            Address = "Kochchikade"
        });

        db.AppUsers.Add(new AppUser
        {
            UserId = _userId,
            FullName = "Rajitha Dassanayake",
            Email = "rajitha@example.com",
            AuthProvider = (short)AuthProvider.Email,
            PreferredLanguage = _englishId,
            IsActive = true,
            NotifyEvents = true,
            NotifyAnnouncements = true,
            NotifyMassReminders = true,
            NotifyFeastDays = true,
            CreatedAt = DateTime.UtcNow
        });

        db.UserFollowedLocations.Add(new UserFollowedLocation
        {
            UserId = _userId,
            LocationId = _locationId,
            FollowedAt = DateTime.UtcNow
        });

        // NOTE: Sunday 06:30, the shape most of the mass logic is written around.
        db.MassSchedules.Add(new MassSchedule
        {
            ScheduleId = _scheduleId,
            LocationId = _locationId,
            DayOfWeek = 0,
            MassTime = new TimeOnly(6, 30),
            IsSpecial = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        db.MassScheduleTranslations.Add(new MassScheduleTranslation
        {
            TranslationId = Guid.NewGuid(),
            ScheduleId = _scheduleId,
            LanguageId = _englishId,
            Label = "Sinhala Mass"
        });

        db.UserMassReminders.Add(new UserMassReminder
        {
            ReminderId = _reminderId,
            UserId = _userId,
            ScheduleId = _scheduleId,
            MinutesBefore = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        db.LiturgicalCalendars.Add(new LiturgicalCalendar
        {
            CalendarId = _calendarId,
            Month = 8,
            Day = 15,
            IsRecurringAnnually = true,
            IsHolyDay = true,
            CreatedAt = DateTime.UtcNow
        });

        db.LocationFeastDays.Add(new LocationFeastDay
        {
            LocationFeastDayId = _feastDayId,
            LocationId = _locationId,
            CalendarId = _calendarId,
            IsHighlighted = true,
            CreatedAt = DateTime.UtcNow
        });

        // NOTE: One delivered row per user-facing type, so every branch of the inbox projection is exercised.
        db.NotificationsLogs.AddRange(
            new NotificationsLog
            {
                NotificationId = Guid.NewGuid(),
                UserId = _userId,
                Type = (short)NotificationType.MassReminder,
                ReferenceId = _scheduleId,
                OccurrenceDate = new DateOnly(2026, 8, 9),
                Title = "St. Anthony's Shrine",
                Body = "Sinhala Mass starts in 30 minutes.",
                IsSent = true,
                SentAt = new DateTime(2026, 8, 9, 0, 30, 0, DateTimeKind.Utc),
                IsRead = false
            },
            new NotificationsLog
            {
                NotificationId = Guid.NewGuid(),
                UserId = _userId,
                Type = (short)NotificationType.FeastDay,
                ReferenceId = _feastDayId,
                Title = "Feast day",
                Body = "The Assumption",
                IsSent = true,
                SentAt = new DateTime(2026, 8, 15, 3, 0, 0, DateTimeKind.Utc),
                IsRead = false
            });

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // IMPORTANT: TEST 1 - The densest projection in the codebase; proves it translates
    [Fact]
    public async Task UserNotificationRepository_GetForUser_TranslatesAndResolvesEveryReference()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new UserNotificationRepository(db);

        // NOTE: Act
        var results = await repo.GetForUserAsync(_userId, 0, 20);

        // NOTE: Assert — newest first, so the feast day leads
        Assert.Equal(2, results.Count);

        var feast = results[0];
        Assert.Equal((short)NotificationType.FeastDay, feast.Type);
        Assert.Equal(_locationId, feast.LocationId);
        Assert.Equal(_calendarId, feast.CalendarId);
        Assert.Equal(8, feast.FeastMonth);
        Assert.Equal(15, feast.FeastDayOfMonth);

        var mass = results[1];
        Assert.Equal((short)NotificationType.MassReminder, mass.Type);
        Assert.Equal(_locationId, mass.LocationId);
        Assert.Equal(0, mass.MassDayOfWeek);
        Assert.Equal(new TimeOnly(6, 30), mass.MassTime);
        Assert.Equal(new DateOnly(2026, 8, 9), mass.OccurrenceDate);
    }

    // IMPORTANT: TEST 2 - The unread count must use the same predicate as the list, or the badge lies
    [Fact]
    public async Task UserNotificationRepository_GetUnreadCount_MatchesTheList()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new UserNotificationRepository(db);

        // NOTE: Act
        var count = await repo.GetUnreadCountAsync(_userId);
        var list = await repo.GetForUserAsync(_userId, 0, 100);

        // NOTE: Assert
        Assert.Equal(list.Count(r => !r.IsRead), count);
    }

    // IMPORTANT: TEST 3 - The mass query carries labels, church names and the reminder
    [Fact]
    public async Task MassScheduleRepository_GetForFollowedLocations_ReturnsPatternWithReminder()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new MassScheduleRepository(db);

        // NOTE: Act
        var results = await repo.GetForFollowedLocationsAsync(_userId, "en", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        // NOTE: Assert
        var pattern = Assert.Single(results);
        Assert.Equal(_scheduleId, pattern.ScheduleId);
        Assert.Equal("St. Anthony's Shrine", pattern.LocationName);
        Assert.Equal("Sinhala Mass", pattern.Label);
        Assert.NotNull(pattern.Reminder);
        Assert.Equal(30, pattern.Reminder.MinutesBefore);
    }

    // IMPORTANT: TEST 4 - Location detail folds in follow state and the caller's reminders in one round trip
    [Fact]
    public async Task LocationRepository_GetLocationById_ResolvesFollowStateAndReminders()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new LocationRepository(db);

        // NOTE: Act
        var signedIn = await repo.GetLocationByIdAsync(_locationId, "en", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), _userId);
        var anonymous = await repo.GetLocationByIdAsync(_locationId, "en", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null);

        // NOTE: Assert
        Assert.NotNull(signedIn);
        Assert.True(signedIn.IsFollowing);
        Assert.NotNull(Assert.Single(signedIn.Schedules).Reminder);

        Assert.NotNull(anonymous);
        Assert.False(anonymous.IsFollowing);
        Assert.Null(Assert.Single(anonymous.Schedules).Reminder);
    }

    // IMPORTANT: TEST 5 - The reminder list joins through the schedule to the church and reports follow state
    [Fact]
    public async Task MassReminderRepository_GetForUser_ReturnsAgendaRow()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new MassReminderRepository(db);

        // NOTE: Act
        var results = await repo.GetForUserAsync(_userId, "en");

        // NOTE: Assert
        var row = Assert.Single(results);
        Assert.Equal(_reminderId, row.ReminderId);
        Assert.Equal("St. Anthony's Shrine", row.LocationName);
        Assert.Equal("Sinhala Mass", row.Label);
        Assert.True(row.IsFollowing);
        Assert.True(row.IsActive);
    }

    // IMPORTANT: TEST 6 - Cancelling by location updates through a navigation property
    [Fact]
    public async Task MassReminderRepository_DisableForLocation_SwitchesThemOff()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new MassReminderRepository(db);

        // NOTE: Act
        var affected = await repo.DisableForLocationAsync(_userId, _locationId);

        // NOTE: Assert
        Assert.Equal(1, affected);

        using var verifyDb = _fixture.CreateContext();
        var after = await new MassReminderRepository(verifyDb).GetForUserAsync(_userId, "en");
        Assert.False(Assert.Single(after).IsActive);
    }

    // IMPORTANT: TEST 7 - The push job's candidate query filters on five separate live-ness rules
    [Fact]
    public async Task MassReminderNotificationRepository_GetActiveReminders_ReturnsLiveOnes()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new MassReminderNotificationRepository(db);

        // NOTE: Act
        var due = await repo.GetActiveRemindersAsync([0]);
        var otherDay = await repo.GetActiveRemindersAsync([3]);

        // NOTE: Assert
        var reminder = Assert.Single(due);
        Assert.Equal(_scheduleId, reminder.ScheduleId);
        Assert.Equal("Sinhala Mass", reminder.Label);
        Assert.Equal("St. Anthony's Shrine", reminder.LocationName);
        Assert.Equal("en", reminder.LanguageCode);
        Assert.Empty(otherDay);
    }

    // IMPORTANT: TEST 8 - Dedup reads back the (user, schedule, date) triple the job writes
    [Fact]
    public async Task MassReminderNotificationRepository_GetAlreadyNotified_FindsTheSeededRow()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new MassReminderNotificationRepository(db);

        // NOTE: Act
        var already = await repo.GetAlreadyNotifiedAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        // NOTE: Assert
        Assert.Contains((_userId, _scheduleId, new DateOnly(2026, 8, 9)), already);
    }

    // IMPORTANT: TEST 9 - Pending delivery resolves the church through the schedule
    [Fact]
    public async Task MassReminderNotificationRepository_GetPending_ResolvesLocationThroughSchedule()
    {
        // NOTE: Arrange — the seeded reminder row is already sent, so queue an unsent one
        using var db = _fixture.CreateContext();
        db.NotificationsLogs.Add(new NotificationsLog
        {
            NotificationId = Guid.NewGuid(),
            UserId = _userId,
            Type = (short)NotificationType.MassReminder,
            ReferenceId = _scheduleId,
            OccurrenceDate = new DateOnly(2026, 8, 16),
            Title = "St. Anthony's Shrine",
            Body = "Sinhala Mass starts in 30 minutes.",
            IsSent = false,
            IsRead = false
        });
        await db.SaveChangesAsync();

        var repo = new MassReminderNotificationRepository(db);

        // NOTE: Act
        var pending = await repo.GetPendingAsync(10);

        // NOTE: Assert
        var item = Assert.Single(pending);
        Assert.Equal(_locationId, item.LocationId);
        Assert.Equal(new DateOnly(2026, 8, 16), item.OccurrenceDate);
    }

    // IMPORTANT: TEST 10 - The pin colour is carried from the type through both location payloads
    [Fact]
    public async Task LocationRepository_CarriesPinColourFromTheType()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new LocationRepository(db);

        // NOTE: Act
        var detail = await repo.GetLocationByIdAsync(_locationId, "en", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 7), _userId);
        var list = await repo.GetActiveLocationsAsync("en", null, null, null, null, null, null, null, null, null, _userId);
        var followed = await repo.GetFollowedLocationsAsync(_userId, "en", null, null);

        // NOTE: Assert
        Assert.Equal("#B23A48", detail!.PinColorHex);
        Assert.Equal("#B23A48", Assert.Single(list).PinColorHex);
        Assert.Equal("#B23A48", Assert.Single(followed).PinColorHex);
    }

    // IMPORTANT: TEST 11 - The feast job's candidate query and its anti-join both have to reach SQL
    [Fact]
    public async Task FeastDayNotificationRepository_FindsPinnedFeastsAndDedupesRecipients()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new FeastDayNotificationRepository(db);
        var occurrence = new DateOnly(2026, 8, 15);

        // NOTE: Act
        var feasts = await repo.GetPinnedFeastDaysAsync();
        var before = await repo.GetRecipientsWithoutLogAsync(_feastDayId, _locationId, occurrence, default);

        db.NotificationsLogs.Add(new NotificationsLog
        {
            NotificationId = Guid.NewGuid(),
            UserId = _userId,
            Type = (short)NotificationType.FeastDay,
            ReferenceId = _feastDayId,
            OccurrenceDate = occurrence,
            Title = "The Assumption",
            Body = "Holy Day of Obligation",
            IsSent = false,
            IsRead = false
        });
        await db.SaveChangesAsync();

        var after = await repo.GetRecipientsWithoutLogAsync(_feastDayId, _locationId, occurrence, default);

        // NOTE: Assert
        var feast = Assert.Single(feasts);
        Assert.Equal(_calendarId, feast.CalendarId);
        Assert.True(feast.IsRecurringAnnually);
        Assert.Equal(8, feast.Month);

        Assert.Equal(_userId, Assert.Single(before).UserId);
        Assert.Empty(after);
    }

    // IMPORTANT: TEST 12 - Fetching one announcement applies the same rules as the list
    [Fact]
    public async Task AnnouncementRepository_GetAnnouncement_RespectsExpiry()
    {
        // NOTE: Arrange — announcements are authored by an admin, so the FK needs one to point at.
        using var db = _fixture.CreateContext();
        var liveId = Guid.NewGuid();
        var expiredId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.AdminUsers.Add(new AdminUser
        {
            AdminId = adminId,
            FullName = "Parish Admin",
            Email = "admin@example.com",
            PasswordHash = "x",
            Role = 1,
            IsActive = true,
            CreatedAt = now
        });

        db.Announcements.AddRange(
            new Announcement
            {
                AnnouncementId = liveId,
                LocationId = _locationId,
                MediaType = 1,
                MediaUrl = "https://parishbell.blob.core.windows.net/announcements/live.mp3?sv=expired",
                DurationSeconds = 90,
                IsActive = true,
                CreatedAt = now,
                ExpiresAt = now.AddHours(4),
                CreatedBy = adminId
            },
            new Announcement
            {
                AnnouncementId = expiredId,
                LocationId = _locationId,
                MediaType = 1,
                MediaUrl = "https://parishbell.blob.core.windows.net/announcements/old.mp3",
                DurationSeconds = 30,
                IsActive = true,
                CreatedAt = now.AddDays(-2),
                ExpiresAt = now.AddHours(-1),
                CreatedBy = adminId
            });
        await db.SaveChangesAsync();

        var repo = new AnnouncementRepository(db);

        // NOTE: Act
        var live = await repo.GetAnnouncementAsync(liveId, "en", now);
        var expired = await repo.GetAnnouncementAsync(expiredId, "en", now);

        // NOTE: Assert
        Assert.NotNull(live);
        Assert.Equal(_locationId, live.LocationId);
        Assert.Null(expired);
    }

    // IMPORTANT: TEST 13 - Marking read is a set-based update scoped to the owner
    [Fact]
    public async Task UserNotificationRepository_MarkAllRead_ClearsOnlyThisUser()
    {
        // NOTE: Arrange
        using var db = _fixture.CreateContext();
        var repo = new UserNotificationRepository(db);

        // NOTE: Act
        var affected = await repo.MarkAllReadAsync(_userId);

        // NOTE: Assert
        Assert.Equal(2, affected);
        Assert.Equal(0, await repo.GetUnreadCountAsync(_userId));
    }
}
