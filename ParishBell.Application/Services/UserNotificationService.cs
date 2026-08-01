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

            // NOTE: One polymorphic reference_id fans out into the typed id the client deep-links on. Mass reminders point at a schedule, which the client reaches through locationId.
            EventId = type == NotificationType.Event ? result.ReferenceId : null,
            AnnouncementId = type == NotificationType.Announcement ? result.ReferenceId : null,
            CalendarId = result.CalendarId
        };
    }

    // NOTE: DB timestamps are stored as UTC - emit an explicit "Z" so the client parses them unambiguously.
    private static string FormatUtc(DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'");
}
