namespace FurniSpace.Application.Common.ProductVersions;

internal static class ProductVersionTaxRateValidator
{
    internal const decimal MinTaxRate = 0m;
    internal const decimal MaxTaxRate = 100m;

    internal static bool IsValid(decimal? defaultTaxRate)
    {
        if (!defaultTaxRate.HasValue)
        {
            return true;
        }

        return defaultTaxRate.Value is >= MinTaxRate and <= MaxTaxRate;
    }
}
