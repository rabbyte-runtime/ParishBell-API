namespace ParishBell.Core.DTOs.Common;

// NOTE: Name is already resolved with English fallback by the repository.
// NOTE: PinColorHex is stored per type, not derived from SortOrder. See LocationType.
public record LocationTypeResult(Guid LocationTypeId, string LocationTypeCode, int SortOrder, string Name, string? PinColorHex);