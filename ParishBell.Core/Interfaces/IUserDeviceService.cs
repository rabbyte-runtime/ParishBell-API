using ParishBell.Core.DTOs.User;

namespace ParishBell.Core.Interfaces;

public interface IUserDeviceService
{
    Task RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequestDto request, CancellationToken ct = default);

    Task RemoveDeviceTokenAsync(Guid userId, string token, CancellationToken ct = default);
}
