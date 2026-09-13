namespace FurniSpace.Infrastructure.Repositories.Repository;

internal static class CatalogKeywordMatcher
{
    internal static bool Matches(string? productName, string? productCode, string keyword)
    {
        return ContainsIgnoreCase(productName, keyword) ||
               ContainsIgnoreCase(productCode, keyword);
    }

    private static bool ContainsIgnoreCase(string? value, string keyword)
    {
        return !string.IsNullOrEmpty(value) &&
               value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
