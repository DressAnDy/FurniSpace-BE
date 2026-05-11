using FurniSpace.Domain.Common;

namespace FurniSpace.Domain.ValueObjects;

public sealed class Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<Email>(Error.Validation("Email.Empty", "Email cannot be empty"));

        if (!email.Contains('@'))
            return Result.Failure<Email>(Error.Validation("Email.Invalid", "Email format is invalid"));

        return Result.Success(new Email(email.ToLowerInvariant()));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
