using ParishBell.Core.DTOs.User;

namespace ParishBell.Core.Interfaces;

public interface IUserService
{
    // NOTE: Profile of the currently signed-in user, resolved from the JWT subject.
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default);

    // NOTE: Applies the supplied fields to the signed-in user and returns the stored profile.
    //       Omitted fields are left alone; a request that changes nothing is a no-op.
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken ct = default);
}
