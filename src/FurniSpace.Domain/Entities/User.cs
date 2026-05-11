using System.Collections.Generic;
using System.Linq;
using FurniSpace.Domain.Common;
using FurniSpace.Domain.Events;
using FurniSpace.Domain.ValueObjects;

namespace FurniSpace.Domain.Entities;

public class User : AggregateRoot
{
    public Email Email { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private readonly List<RefreshToken> _refreshTokens = new();
    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    private User() { }

    public static User Create(Email email, string fullName, string passwordHash)
    {
        var user = new User
        {
            Email = email,
            FullName = fullName,
            PasswordHash = passwordHash,
            IsActive = true
        };

        user.RaiseDomainEvent(new UserCreatedEvent(user.Id, email.Value));
        return user;
    }

    public void AddRefreshToken(RefreshToken token)
    {
        _refreshTokens.ForEach(t => t.Revoke());
        _refreshTokens.Add(token);
        SetUpdatedAt();
    }
}
