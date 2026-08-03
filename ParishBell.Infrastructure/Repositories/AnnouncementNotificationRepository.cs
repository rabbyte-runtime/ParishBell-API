using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class AnnouncementNotificationRepository(ParishBellDbContext dbContext) : IAnnouncementNotificationRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;

    private const short AnnouncementType = (short)NotificationType.Announcement;

    public async Task<IReadOnlyList<PendingAnnouncement>> GetAnnouncementsToNotifyAsync(DateTime createdAfterUtc, DateTime nowUtc, CancellationToken ct = default)
    {
        return await _dbContext.Announcements
            .AsNoTracking()
            .Where(a => a.IsActive && a.ExpiresAt > nowUtc && a.CreatedAt >= createdAfterUtc)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new PendingAnnouncement
            {
                AnnouncementId = a.AnnouncementId,
                LocationId = a.LocationId,
                CreatedAt = a.CreatedAt,
                Translations = a.AnnouncementTranslations
                    .Select(t => new AnnouncementTranslationText
                    {
                        LanguageCode = t.Language.LanguageCode,
                        Title = t.Title,
                        Caption = t.Caption
                    })
                    .ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationRecipient>> GetFollowersWithoutLogAsync(Guid announcementId, Guid locationId, CancellationToken ct = default)
    {
        // NOTE: Followers minus anyone already logged for this announcement (NOT EXISTS).
        // NOTE: Keeps the fan-out idempotent and safe to run every poll.
        return await _dbContext.UserFollowedLocations
            .AsNoTracking()
            .Where(f => f.LocationId == locationId && f.User.IsActive)
            .Where(f => !_dbContext.NotificationsLogs
                .Any(n => n.UserId == f.UserId && n.Type == AnnouncementType && n.ReferenceId == announcementId))
            .Select(f => new NotificationRecipient
            {
                UserId = f.UserId,
                LanguageCode = f.User.PreferredLanguageNavigation.LanguageCode
            })
            .ToListAsync(ct);
    }

    public async Task AddLogsAsync(IReadOnlyCollection<NotificationsLog> logs, CancellationToken ct = default)
    {
        if (logs.Count == 0) return;

        _dbContext.NotificationsLogs.AddRange(logs);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PendingNotification>> GetPendingAsync(short type, int max, CancellationToken ct = default)
    {
        return await _dbContext.NotificationsLogs
            .AsNoTracking()
            .Where(n => n.Type == type && !n.IsSent)
            .OrderBy(n => n.NotificationId)
            .Take(max)
            .Select(n => new PendingNotification
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                Title = n.Title,
                Body = n.Body,
                ReferenceId = n.ReferenceId,

                // NOTE: The log stores only the polymorphic reference, so the church is looked up here.
                // NOTE: Other types resolve to null until their own senders exist.
                LocationId = n.Type == AnnouncementType
                    ? _dbContext.Announcements.Where(a => a.AnnouncementId == n.ReferenceId).Select(a => (Guid?)a.LocationId).FirstOrDefault()
                    : null
            })
            .ToListAsync(ct);
    }

    public async Task MarkSentAsync(IReadOnlyCollection<Guid> notificationIds, DateTime sentAtUtc, CancellationToken ct = default)
    {
        if (notificationIds.Count == 0) return;

        await _dbContext.NotificationsLogs
            .Where(n => notificationIds.Contains(n.NotificationId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsSent, true)
                .SetProperty(n => n.SentAt, sentAtUtc), ct);
    }
}
