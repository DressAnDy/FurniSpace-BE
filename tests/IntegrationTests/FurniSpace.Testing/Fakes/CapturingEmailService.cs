using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Testing.Fakes;

public sealed class CapturingEmailService : IEmailService
{
    public List<CapturedEmail> Messages { get; } = [];

    public Task SendPasswordResetAsync(
        string recipientEmail,
        string recipientName,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        Messages.Add(new CapturedEmail(recipientEmail, recipientName, "PasswordReset", resetToken));
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        Messages.Add(new CapturedEmail(recipientEmail, recipientName, "EmailVerification", otpCode));
        return Task.CompletedTask;
    }
}

public sealed record CapturedEmail(
    string RecipientEmail,
    string RecipientName,
    string Type,
    string Code);
