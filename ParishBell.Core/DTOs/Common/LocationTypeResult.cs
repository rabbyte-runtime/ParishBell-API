namespace ParishBell.Core.DTOs.Common;

// NOTE: Name is already resolved with English fallback by the repository.
public record LocationTypeResult(Guid LocationTypeId, string LocationTypeCode, int SortOrder, string Name);