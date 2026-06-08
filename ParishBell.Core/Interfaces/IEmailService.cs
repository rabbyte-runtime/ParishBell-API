namespace ParishBell.Core.Interfaces;

public interface IEmailService
{
    // NOTE: Send password reset email with 6-digit code
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetCode, string languageCode, CancellationToken ct = default);
}