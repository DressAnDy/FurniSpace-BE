namespace FurniSpace.Infrastructure.Common.Email;

public interface IGmailAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    void Invalidate();
}
