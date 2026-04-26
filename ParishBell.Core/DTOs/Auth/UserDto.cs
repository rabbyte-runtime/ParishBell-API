namespace ParishBell.Core.DTOs.Auth;

public class UserDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid PreferredLanguage { get; set; }
}