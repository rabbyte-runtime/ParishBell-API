using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.User;
using ParishBell.Core.Enums;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class UserNotificationService(IUserNotificationRepository notificationRepository) : IUserNotificationService
{
    private readonly IUserNotificationRepository _notificationRepository = notificationRepository;

    private const int DefaultPageSize = 20;

    // IMPORTANT: The log grows without bound, so the caller cannot ask for the whole thing in one go.
    private const int MaxPageSize = 100;

    public async Task<NotificationPageDto> GetNotificationsAsync(Guid userId, int? page, int? pageSize, CancellationToken ct = default)
    {
        int resolvedPage = page is > 0 ? page.Value : 1;
        int resolvedPageSize = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        // NOTE: Fetch one extra row to detect a next page without a separate COUNT query.
        int skip = (resolvedPage - 1) * resolvedPageSize;
        var results = await _notificationRepository.GetForUserAsync(userId, skip, resolvedPageSize + 1, ct);

        bool hasMore = results.Count > resolvedPageSize;
        if (hasMore) results = [.. results.Take(resolvedPageSize)];

        return new NotificationPageDto
        {
            Items = [.. results.Select(MapToDto)],
            Page = resolvedPage,
            PageSize = resolvedPageSize,
            HasMore = hasMore
        };
    }

    public async Task<NotificationUnreadCountDto> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return new NotificationUnreadCountDto
        {
            UnreadCount = await _notificationRepository.GetUnreadCountAsync(userId, ct)
        };
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        // IMPORTANT: Someone else's notification id is indistinguishable from a missing one - both 404.
        if (!await _notificationRepository.MarkReadAsync(userId, notificationId, ct))
            throw new NotFoundException(MessageCodes.GeneralNotFound);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _notificationRepository.MarkAllReadAsync(userId, ct);
    }

    private static NotificationDto MapToDto(NotificationResult result)
    {
        var type = (NotificationType)result.Type;

        return new NotificationDto
        {
            NotificationId = result.NotificationId,
            Type = type.ToString(),
            Title = result.Title,
            Body = result.Body,
            SentAt = FormatUtc(result.SentAt),
            IsRead = result.IsRead,
            LocationId = result.LocationId,

            // NOTE: One polymorphic reference_id fans out into the typed id the client deep-links on.
            EventId = type == NotificationType.Event ? result.ReferenceId : null,
            AnnouncementId = type == NotificationType.Announcement ? result.ReferenceId : null,
            ScheduleId = type == NotificationType.MassReminder ? result.ReferenceId : null,
            CalendarId = result.CalendarId,

            // NOTE: Without this a mass-reminder row has nowhere to go but the church, and a feast row nowhere at all.
            Date = ResolveDate(type, result)?.ToString("yyyy-MM-dd")
        };
    }

    // NOTE: The queueing job records the day it notified about, so that is used when present.
    // NOTE: The reconstruction below is the fallback for rows written before occurrence_date existed.
    private static DateOnly? ResolveDate(NotificationType type, NotificationResult result) => result.OccurrenceDate ?? type switch
    {
        NotificationType.MassReminder => ResolveMassDate(result),
        NotificationType.FeastDay => ResolveFeastDate(result),

        // NOTE: Events and announcements carry a typed id the client fetches instead - a date would add nothing.
        _ => null
    };

    // NOTE: The push fires shortly *before* the mass, so the occurrence is the first matching weekday at or after it was sent.
    private static DateOnly? ResolveMassDate(NotificationResult result)
    {
        if (result.MassDayOfWeek is not { } dayOfWeek || result.MassTime is not { } massTime)
            return null;

        var sentDate = DateOnly.FromDateTime(result.SentAt);

        // NOTE: DayOfWeek is 0=Sunday..6=Saturday in the DB, which is exactly System.DayOfWeek's own numbering.
        var shift = ((dayOfWeek - (int)sentDate.DayOfWeek) + 7) % 7;
        var date = sentDate.AddDays(shift);

        // NOTE: Landing on the send day only counts if the mass had not already started - otherwise it is next week's.
        if (shift == 0 && massTime < TimeOnly.FromDateTime(result.SentAt))
            date = date.AddDays(7);

        return date;
    }

    // NOTE: A fixed feast carries its own date; a recurring one is a month/day that has to be placed in a year.
    private static DateOnly? ResolveFeastDate(NotificationResult result)
    {
        if (result.FeastSpecificDate is { } specificDate)
            return specificDate;

        if (result.FeastMonth is not { } month || result.FeastDayOfMonth is not { } day)
            return null;

        var sentDate = DateOnly.FromDateTime(result.SentAt);
        var resolved = BuildDate(sentDate.Year, month, day);

        // NOTE: A feast notified in late December for a January date belongs to the next year, not the one just ending.
        if (resolved is { } valid && valid < sentDate.AddDays(-1))
            return BuildDate(sentDate.Year + 1, month, day);

        return resolved;
    }

    // NOTE: Null rather than throwing on a day that does not exist that year - Feb 29 outside a leap year.
    private static DateOnly? BuildDate(int year, int month, int day)
    {
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            return null;

        return new DateOnly(year, month, day);
    }

    // NOTE: DB timestamps are stored as UTC - emit an explicit "Z" so the client parses them unambiguously.
    private static string FormatUtc(DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'");
}
