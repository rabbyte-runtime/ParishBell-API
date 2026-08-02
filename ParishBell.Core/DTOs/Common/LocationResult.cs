namespace ParishBell.Core.DTOs.Common;

// NOTE: Name and Address are already resolved with English fallback by the repository.
public record LocationResult(Guid LocationId, decimal Latitude, decimal Longitude, Guid LocationTypeId, string Name, string? Address,
string? Phone, string? Email, string? Website);
