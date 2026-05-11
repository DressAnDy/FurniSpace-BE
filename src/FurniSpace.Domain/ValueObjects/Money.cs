namespace FurniSpace.Domain.ValueObjects;

public sealed class Money
{
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
}
