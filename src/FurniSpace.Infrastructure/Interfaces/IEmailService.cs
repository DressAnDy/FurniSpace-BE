namespace FurniSpace.Infrastructure.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetAsync(string recipientEmail, string recipientName, string resetToken, CancellationToken cancellationToken = default);
    Task SendEmailVerificationOtpAsync(string recipientEmail, string recipientName, string otpCode, CancellationToken cancellationToken = default);
}
