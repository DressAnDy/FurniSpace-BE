namespace FurniSpace.Application.Common.Validation;

public static class BusinessDimensionValidator
{
    public static bool IsNullOrPositive(decimal? value)
    {
        return !value.HasValue || value.Value > 0m;
    }

    public static bool IsStrictlyPositive(decimal? value)
    {
        return value.HasValue && value.Value > 0m;
    }

    public static bool IsNonNegative(decimal? value)
    {
        return !value.HasValue || value.Value >= 0m;
    }
}
