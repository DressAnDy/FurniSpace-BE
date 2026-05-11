using System;

namespace FurniSpace.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Token { get; private set; }
    public bool IsRevoked { get; private set; }

    public RefreshToken(string token)
    {
        Token = token;
    }

    public void Revoke() => IsRevoked = true;
}
