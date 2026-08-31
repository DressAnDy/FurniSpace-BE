using FurniSpace.Application.Common.Orders;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderListPaginationSupportTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void ValidatePagination_WhenInvalid_ReturnsMessage(int page, int pageSize)
    {
        var message = OrderListPaginationSupport.ValidatePagination(page, pageSize);

        Assert.NotNull(message);
    }

    [Fact]
    public void ValidatePagination_WhenValid_ReturnsNull()
    {
        var message = OrderListPaginationSupport.ValidatePagination(1, 20);

        Assert.Null(message);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(25, 10, 3)]
    [InlineData(21, 10, 3)]
    public void CalculateTotalPages_ReturnsExpectedValue(int totalCount, int pageSize, int expectedPages)
    {
        Assert.Equal(expectedPages, OrderListPaginationSupport.CalculateTotalPages(totalCount, pageSize));
    }
}
