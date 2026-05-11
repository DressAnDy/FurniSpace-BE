using FurniSpace.Domain.Entities;

namespace FurniSpace.Domain.Specifications;

public sealed class ActiveUserByEmailSpec : BaseSpecification<User>
{
    public ActiveUserByEmailSpec(string email)
    {
        Criteria = u => u.Email.Value == email.ToLower() && u.IsActive && !u.IsDeleted;
    }
}
