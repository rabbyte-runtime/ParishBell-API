namespace ParishBell.Core.DTOs.Common;

// NOTE: Name and Address are already resolved with English fallback by the repository.
// NOTE: IsFollowing is always false for an anonymous caller - these endpoints are public, so "not following" and "nobody asked" look the same.
public record LocationResult(Guid LocationId, decimal Latitude, decimal Longitude, Guid LocationTypeId, string Name, string? Address,
string? Phone, string? Email, string? Website, bool IsFollowing, string? PinColorHex);
