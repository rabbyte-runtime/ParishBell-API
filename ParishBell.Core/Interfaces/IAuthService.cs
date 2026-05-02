using ParishBell.Core.DTOs.Auth;

namespace ParishBell.Core.Interfaces;

public interface IAuthService
{
    // NOTE: Registers a new mobile app user
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, string ipAddress, CancellationToken ct = default);
}