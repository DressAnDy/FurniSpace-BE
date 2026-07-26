using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurniSpace.Infrastructure.Common.Email;

public sealed class GmailApiEmailService : IEmailService
{
    private const int MaxProviderErrorLength = 1_000;
    private readonly HttpClient _httpClient;
    private readonly IGmailAccessTokenProvider _tokenProvider;
    private readonly GmailApiSettings _settings;
    private readonly ILogger<GmailApiEmailService> _logger;

    public GmailApiEmailService(
        HttpClient httpClient,
        IGmailAccessTokenProvider tokenProvider,
        IOptions<GmailApiSettings> settings,
        ILogger<GmailApiEmailService> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task SendPasswordResetAsync(
        string recipientEmail,
        string recipientName,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        var resetInstructions = BuildResetInstructions(recipientEmail, resetToken);
        var textContent =
            $"Hello {recipientName},\n\nUse these details to reset your password:\n{resetInstructions}\n\nThis token expires in 30 minutes.";

        return SendEmailAsync(
            recipientEmail,
            recipientName,
            "Reset your FurniSpace password",
            textContent,
            htmlContent: null,
            cancellationToken);
    }

    public Task SendEmailVerificationOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        var displayName = string.IsNullOrWhiteSpace(recipientName) ? "there" : recipientName;
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeOtpCode = WebUtility.HtmlEncode(otpCode);
        var textContent =
            $"Hello {displayName},\n\nYour FurniSpace email verification code is: {otpCode}\n\nThis code expires in 5 minutes. Do not share it with anyone.";
        var htmlContent = $$"""
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
                                            <div class="otp-code">{{safeOtpCode}}</div>
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
            recipientName,
            "FurniSpace - Email Verification OTP",
            textContent,
            htmlContent,
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

    private async Task SendEmailAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string textContent,
        string? htmlContent,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var mimeMessage = BuildMimeMessage(
            recipientEmail,
            recipientName,
            subject,
            textContent,
            htmlContent);
        var rawMessage = ToBase64Url(Encoding.UTF8.GetBytes(mimeMessage));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Post, "users/me/messages/send")
            {
                Content = JsonContent.Create(new GmailSendRequest(rawMessage))
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("Gmail API email request timed out.");
                throw new EmailDeliveryException("The email provider timed out.");
            }
            catch (HttpRequestException exception)
            {
                _logger.LogError(exception, "Gmail API could not be reached.");
                throw new EmailDeliveryException(
                    "The email provider could not be reached.",
                    innerException: exception);
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    _tokenProvider.Invalidate();
                    continue;
                }

                var providerMessage = await ReadProviderErrorAsync(response, cancellationToken);
                _logger.LogError(
                    "Gmail API rejected an email request with status code {StatusCode}.",
                    (int)response.StatusCode);
                throw new EmailDeliveryException(
                    $"The email provider rejected the request with status code {(int)response.StatusCode}.",
                    response.StatusCode,
                    providerMessage);
            }
        }

        throw new EmailDeliveryException("Gmail API authentication failed.");
    }

    private string BuildMimeMessage(
        string recipientEmail,
        string recipientName,
        string subject,
        string textContent,
        string? htmlContent)
    {
        var senderAddress = NormalizeEmailAddress(_settings.SenderEmail);
        var recipientAddress = NormalizeEmailAddress(recipientEmail);
        var boundary = $"furnispace_{Guid.NewGuid():N}";
        var builder = new StringBuilder();

        builder.Append("From: ")
            .Append(EncodeHeader(_settings.SenderName))
            .Append(" <")
            .Append(senderAddress)
            .Append(">\r\n")
            .Append("To: ")
            .Append(EncodeHeader(recipientName))
            .Append(" <")
            .Append(recipientAddress)
            .Append(">\r\n")
            .Append("Subject: ")
            .Append(EncodeHeader(subject))
            .Append("\r\n")
            .Append("MIME-Version: 1.0\r\n")
            .Append("Content-Type: multipart/alternative; boundary=\"")
            .Append(boundary)
            .Append("\"\r\n\r\n");

        AppendMimePart(builder, boundary, "text/plain", textContent);
        if (!string.IsNullOrWhiteSpace(htmlContent))
        {
            AppendMimePart(builder, boundary, "text/html", htmlContent);
        }

        builder.Append("--").Append(boundary).Append("--\r\n");
        return builder.ToString();
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail))
        {
            throw new EmailDeliveryException(
                "Gmail API is not configured. Set GmailApi__SenderEmail.");
        }
    }

    private static void AppendMimePart(
        StringBuilder builder,
        string boundary,
        string mediaType,
        string content)
    {
        builder.Append("--")
            .Append(boundary)
            .Append("\r\nContent-Type: ")
            .Append(mediaType)
            .Append("; charset=UTF-8\r\n")
            .Append("Content-Transfer-Encoding: base64\r\n\r\n")
            .Append(WrapBase64(Convert.ToBase64String(Encoding.UTF8.GetBytes(content))))
            .Append("\r\n");
    }

    private static string NormalizeEmailAddress(string email)
    {
        if (email.Contains('\r') || email.Contains('\n'))
        {
            throw new EmailDeliveryException("Email address contains invalid characters.");
        }

        try
        {
            return new MailAddress(email).Address;
        }
        catch (FormatException exception)
        {
            throw new EmailDeliveryException("Email address is invalid.", innerException: exception);
        }
    }

    private static string EncodeHeader(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "FurniSpace user" : value.Trim();
        if (normalized.Contains('\r') || normalized.Contains('\n'))
        {
            throw new EmailDeliveryException("Email header contains invalid characters.");
        }

        return $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(normalized))}?=";
    }

    private static string WrapBase64(string value)
    {
        const int lineLength = 76;
        var builder = new StringBuilder(value.Length + value.Length / lineLength * 2);
        for (var index = 0; index < value.Length; index += lineLength)
        {
            var length = Math.Min(lineLength, value.Length - index);
            builder.Append(value, index, length).Append("\r\n");
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static async Task<string?> ReadProviderErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return content.Length <= MaxProviderErrorLength
            ? content
            : content[..MaxProviderErrorLength];
    }

    private sealed record GmailSendRequest(string Raw);
}
