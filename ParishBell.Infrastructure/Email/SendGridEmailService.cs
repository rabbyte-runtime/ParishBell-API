using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParishBell.Core.Configuration;
using ParishBell.Core.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace ParishBell.Infrastructure.Email;

public class SendGridEmailService(IOptions<SendGridSettings> options, IWebHostEnvironment env, ILogger<SendGridEmailService> logger) : IEmailService
{
    private readonly SendGridSettings _settings = options.Value;
    private readonly IWebHostEnvironment _env = env;
    private readonly ILogger<SendGridEmailService> _logger = logger;

    // NOTE: Send password reset email with 6-digit code
    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetCode, string languageCode, CancellationToken ct = default)
    {
        // NOTE: Build the email content from HTML template
        var htmlBody = await LoadEmailTemplateAsync(languageCode, ct);
        htmlBody = htmlBody.Replace("{{name}}", toName).Replace("{{code}}", resetCode);

        // NOTE: Localized subject line
        var subject = GetSubjectForLanguage(languageCode);

        // IMPORTANT: MockMode - log instead of sending (for dev/testing without real emails)
        if (_settings.MockMode)
        {
            _logger.LogWarning("════════════════════════════════════════");
            _logger.LogWarning("MOCK EMAIL - Password Reset");
            _logger.LogWarning("To: {Email} ({Name})", toEmail, toName);
            _logger.LogWarning("Subject: {Subject}", subject);
            _logger.LogWarning("Reset code: {Code} (expires in 15 minutes)", resetCode);
            _logger.LogWarning("Language: {Lang}", languageCode);
            _logger.LogWarning("════════════════════════════════════════");
            return;
        }

        // NOTE: Send via SendGrid
        var client = new SendGridClient(_settings.ApiKey);
        var from = new EmailAddress(_settings.SenderEmail, _settings.SenderName);
        var to = new EmailAddress(toEmail, toName);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: null, htmlContent: htmlBody);
        var response = await client.SendEmailAsync(msg, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(ct);
            _logger.LogError("SendGrid send failed. Status: {Status}. Body: {Body}", response.StatusCode, body);
            throw new InvalidOperationException("Failed to send password reset email.");
        }

        _logger.LogInformation("Password reset email sent to {Email}", toEmail);
    }

    // NOTE: Load HTML template from disk for the requested language
    // IMPORTANT: Falls back to English if requested language template doesn't exist
    private async Task<string> LoadEmailTemplateAsync(string languageCode, CancellationToken ct)
    {
        var templatePath = Path.Combine(_env.ContentRootPath, "EmailTemplates", $"password-reset-{languageCode}.html");

        if (!File.Exists(templatePath))
        {
            _logger.LogWarning("Email template not found for '{Lang}'. Falling back to English.", languageCode);
            templatePath = Path.Combine(_env.ContentRootPath, "EmailTemplates", "password-reset-en.html");
        }

        return await File.ReadAllTextAsync(templatePath, Encoding.UTF8, ct);
    }

    // NOTE: Localized email subject line
    private static string GetSubjectForLanguage(string languageCode) => languageCode switch
    {
        "si" => "ParishBell - මුරපදය යළි පිහිටුවීමේ කේතය",
        "ta" => "ParishBell - கடவுச்சொல் மீட்டமைப்பு குறியீடு",
        _ => "ParishBell - Your password reset code"
    };
}