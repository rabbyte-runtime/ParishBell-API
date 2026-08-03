using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Mass;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class MassReminderService(IMassReminderRepository reminderRepository) : IMassReminderService
{
    private readonly IMassReminderRepository _reminderRepository = reminderRepository;

    public async Task<UserMassReminderListDto> GetRemindersAsync(Guid userId, string languageCode, CancellationToken ct = default)
    {
        var results = await _reminderRepository.GetForUserAsync(userId, languageCode, ct);

        var items = results.Select(r => new UserMassReminderDto
        {
            ReminderId = r.ReminderId,
            MinutesBefore = r.MinutesBefore,
            IsActive = r.IsActive,
            ScheduleId = r.ScheduleId,
            LocationId = r.LocationId,
            LocationName = r.LocationName,
            DayOfWeek = r.DayOfWeek,
            MassTime = r.MassTime.ToString("HH:mm"),
            Label = r.Label,
            IsSpecial = r.IsSpecial,

            // NOTE: A weekly mass has no window, so the client gets nulls rather than dates it must ignore.
            ValidFrom = r.ValidFrom?.ToString("yyyy-MM-dd"),
            ValidTo = r.ValidTo?.ToString("yyyy-MM-dd"),

            IsFollowing = r.IsFollowing
        }).ToList();

        return new UserMassReminderListDto { Items = items };
    }

    public async Task<MassReminderDto> SetReminderAsync(Guid userId, SetMassReminderRequestDto request, CancellationToken ct = default)
    {
        // IMPORTANT: Checked first so a stale client cannot target a removed or hidden mass.
        if (!await _reminderRepository.IsScheduleRemindableAsync(request.ScheduleId, ct))
            throw new NotFoundException(MessageCodes.MassScheduleNotFound);

        var result = await _reminderRepository.UpsertAsync(userId, request.ScheduleId, request.MinutesBefore, ct);

        return new MassReminderDto
        {
            ReminderId = result.ReminderId,
            MinutesBefore = result.MinutesBefore,
            IsActive = result.IsActive
        };
    }

    public async Task RemoveReminderAsync(Guid userId, Guid reminderId, CancellationToken ct = default)
    {
        // IMPORTANT: Someone else's reminder id is indistinguishable from a missing one - both 404.
        if (!await _reminderRepository.DisableAsync(userId, reminderId, ct))
            throw new NotFoundException(MessageCodes.GeneralNotFound);
    }
}
