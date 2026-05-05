using ParishBell.Core.Enums;

namespace ParishBell.Core.Interfaces;

// NOTE: Common interface for all external auth providers
// IMPORTANT: Apple, Microsoft, etc. will implement this interface later

public interface IExternalAuthValidator
{
    // NOTE: Which provider this validator handles
    AuthProvider Provider { get; }

    // NOTE: Validate the ID token and return verified user info
    Task<ExternalAuthResult> ValidateAsync(string idToken, CancellationToken ct = default);
}

// NOTE: Result of a successful external auth validation
public class ExternalAuthResult
{
    public string ProviderUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
}