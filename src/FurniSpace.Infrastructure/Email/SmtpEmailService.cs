using System.Net;
using System.Net.Mail;
using FurniSpace.Infrastructure.Common.Email;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;

namespace FurniSpace.Infrastructure.Email;

public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;

    public SmtpEmailService(IOptions<SmtpSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendPasswordResetAsync(
        string recipientEmail,
        string recipientName,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var resetLink = BuildResetLink(recipientEmail, resetToken);

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = "Reset your FurniSpace password",
            Body = $"Hello {recipientName},\n\nUse this link to reset your password:\n{resetLink}\n\nThis link expires in 30 minutes.",
            IsBodyHtml = false
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    private string BuildResetLink(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(_settings.ResetPasswordUrl))
        {
            return $"Token: {token}\nEmail: {email}";
        }

        var separator = _settings.ResetPasswordUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{_settings.ResetPasswordUrl}{separator}email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.FromEmail))
        {
            throw new InvalidOperationException("SMTP is not configured. Set Smtp__Host and Smtp__FromEmail.");
        }
    }
}
