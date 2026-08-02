using ParishBell.Core.DTOs.User;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class UserDeviceService(IUserDeviceRepository deviceRepository) : IUserDeviceService
{
    private readonly IUserDeviceRepository _deviceRepository = deviceRepository;

    public async Task RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequestDto request, CancellationToken ct = default)
    {
        var appVersion = string.IsNullOrWhiteSpace(request.AppVersion) ? null : request.AppVersion.Trim();
        await _deviceRepository.UpsertAsync(userId, request.Token.Trim(), (short)request.Platform, appVersion, ct);
    }

    public async Task RemoveDeviceTokenAsync(Guid userId, string token, CancellationToken ct = default)
    {
        await _deviceRepository.RemoveByTokenAsync(userId, token.Trim(), ct);
    }
}
