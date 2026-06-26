using System.Net;
using System.Net.Mail;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;

namespace FurniSpace.Infrastructure.Common.Email;

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
        var resetInstructions = BuildResetInstructions(recipientEmail, resetToken);

        await SendEmailAsync(
            recipientEmail,
            "Reset your FurniSpace password",
            $"Hello {recipientName},\n\nUse these details to reset your password:\n{resetInstructions}\n\nThis token expires in 30 minutes.",
            isHtml: false,
            cancellationToken);
    }

    public Task SendEmailVerificationOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(recipientName) ? "there" : recipientName);
        var body = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <style>
                    body { margin: 0; padding: 0; background: #f3eadf; color: #2f241d; }
                    table { border-collapse: collapse; }
                    .shell { width: 100%; background: #f3eadf; padding: 32px 12px; }
                    .card { width: 100%; max-width: 620px; background: #fffaf3; border-radius: 18px; overflow: hidden; box-shadow: 0 14px 40px rgba(78, 52, 32, 0.14); }
                    .hero { background: linear-gradient(135deg, #6f4e37 0%, #9b6a3d 58%, #d9b382 100%); padding: 34px 36px; }
                    .brand { color: #fff8ed; font-family: Georgia, 'Times New Roman', serif; font-size: 30px; font-weight: 700; letter-spacing: 0.5px; margin: 0; }
                    .tagline { color: #f8e4c6; font-family: Arial, sans-serif; font-size: 14px; margin: 8px 0 0; }
                    .content { padding: 36px; font-family: Arial, sans-serif; }
                    .eyebrow { color: #8a5a32; font-size: 12px; font-weight: 700; letter-spacing: 1.8px; margin: 0 0 12px; text-transform: uppercase; }
                    .title { color: #2f241d; font-size: 24px; line-height: 1.3; margin: 0 0 16px; }
                    .copy { color: #5d4a3d; font-size: 15px; line-height: 1.7; margin: 0 0 18px; }
                    .otp-wrap { background: #efe1d0; border: 1px solid #d8bd9a; border-radius: 16px; margin: 28px 0; padding: 18px; }
                    .otp-code { background: #fffaf3; border: 2px dashed #a56a3a; border-radius: 12px; color: #6f4e37; font-size: 34px; font-weight: 800; letter-spacing: 10px; line-height: 1; padding: 22px 10px; text-align: center; }
                    .note { background: #f7efe5; border-left: 4px solid #6f8f72; color: #5d4a3d; font-size: 14px; line-height: 1.6; margin: 0 0 22px; padding: 14px 16px; }
                    .footer { background: #2f241d; color: #d8c5b4; font-family: Arial, sans-serif; font-size: 12px; line-height: 1.6; padding: 22px 36px; text-align: center; }
                </style>
            </head>
            <body style="margin:0; padding:0; background:#f3eadf;">
                <table role="presentation" class="shell" width="100%" cellpadding="0" cellspacing="0">
                    <tr>
                        <td align="center">
                            <table role="presentation" class="card" cellpadding="0" cellspacing="0">
                                <tr>
                                    <td class="hero">
                                        <p class="brand">FurniSpace</p>
                                        <p class="tagline">Warm wooden interiors, crafted for your space.</p>
                                    </td>
                                </tr>
                                <tr>
                                    <td class="content">
                                        <p class="eyebrow">Email Verification</p>
                                        <h1 class="title">Confirm your FurniSpace account</h1>
                                        <p class="copy">Hello {{safeName}},</p>
                                        <p class="copy">Use the OTP below to verify your email and start designing your interior space with FurniSpace.</p>
                                        <div class="otp-wrap">
                                            <div class="otp-code">{{otpCode}}</div>
                                        </div>
                                        <p class="note">This code will expire in <strong>5 minutes</strong>. For your security, do not share this OTP with anyone.</p>
                                        <p class="copy">If you did not create a FurniSpace account, you can safely ignore this email.</p>
                                    </td>
                                </tr>
                                <tr>
                                    <td class="footer">
                                        &copy; 2026 FurniSpace. Interior wood design for modern living.
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;

        return SendEmailAsync(
            recipientEmail,
            "FurniSpace - Email Verification OTP",
            body,
            isHtml: true,
            cancellationToken);
    }

    private string BuildResetInstructions(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(_settings.ResetPasswordUrl))
        {
            return $"Token: {token}\nEmail: {email}";
        }

        return $"Reset page: {_settings.ResetPasswordUrl}\nEmail: {email}\nToken: {token}";
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.FromEmail))
        {
            throw new InvalidOperationException("SMTP is not configured. Set Smtp__Host and Smtp__FromEmail.");
        }
    }

    private async Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string body,
        bool isHtml,
        CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
