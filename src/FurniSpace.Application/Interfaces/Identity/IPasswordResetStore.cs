namespace FurniSpace.Application.Interfaces.Identity;

public interface IPasswordResetStore
{
    Task<string> CreateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ConsumeAsync(Guid userId, string token, CancellationToken cancellationToken = default);
}
