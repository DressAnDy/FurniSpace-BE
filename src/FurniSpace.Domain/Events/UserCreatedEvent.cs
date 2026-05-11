using System;
using FurniSpace.Domain.Common;

namespace FurniSpace.Domain.Events;

public sealed class UserCreatedEvent : IDomainEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; }

    public UserCreatedEvent(Guid userId, string email)
    {
        UserId = userId;
        Email = email;
    }
}
