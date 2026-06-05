namespace FurniSpace.Application.Interfaces.Identity;

public interface IEmailOtpStore
{
    Task<string> CreateAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ConsumeAsync(string email, string otpCode, CancellationToken cancellationToken = default);
}
