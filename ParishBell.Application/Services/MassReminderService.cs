using ParishBell.Core.Constants;
using ParishBell.Core.DTOs.Mass;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class MassReminderService(IMassReminderRepository reminderRepository) : IMassReminderService
{
    private readonly IMassReminderRepository _reminderRepository = reminderRepository;

    public async Task<MassReminderDto> SetReminderAsync(Guid userId, SetMassReminderRequestDto request, CancellationToken ct = default)
    {
        // IMPORTANT: Checked before the write so a stale client cannot hang a reminder off a mass that has since been removed or hidden.
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
