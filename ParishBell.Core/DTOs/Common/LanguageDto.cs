namespace ParishBell.Core.DTOs.Common;

public class LanguageDto
{
    public Guid LanguageId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string NativeName { get; set; } = default!;
}