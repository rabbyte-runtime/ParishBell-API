using ParishBell.Core.DTOs.User;

namespace ParishBell.Core.Interfaces;

public interface IUserService
{
    // NOTE: Profile of the currently signed-in user, resolved from the JWT subject.
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default);
}
