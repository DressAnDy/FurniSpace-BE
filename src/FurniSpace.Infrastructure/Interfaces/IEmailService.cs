namespace FurniSpace.Infrastructure.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetAsync(string recipientEmail, string recipientName, string resetToken, CancellationToken cancellationToken = default);
}
