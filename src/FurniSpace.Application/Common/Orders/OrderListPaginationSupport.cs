namespace FurniSpace.Application.Common.Orders;

public static class OrderListPaginationSupport
{
    public const int MaxPageSize = 100;

    public static string? ValidatePagination(int page, int pageSize)
    {
        if (page < 1)
        {
            return "Page must be greater than zero.";
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            return $"Page size must be between 1 and {MaxPageSize}.";
        }

        return null;
    }

    public static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
