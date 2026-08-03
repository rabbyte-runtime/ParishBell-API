namespace ParishBell.Core.DTOs.Common;

// NOTE: Name is already resolved with English fallback by the repository.
// NOTE: PinColorHex is stored per type rather than derived from SortOrder - see LocationType for why.
public record LocationTypeResult(Guid LocationTypeId, string LocationTypeCode, int SortOrder, string Name, string? PinColorHex);