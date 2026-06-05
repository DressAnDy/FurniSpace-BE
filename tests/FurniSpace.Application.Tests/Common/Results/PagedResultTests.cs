#nullable enable

using FurniSpace.Application.Common;
using Xunit;

namespace FurniSpace.Application.Tests.Common.Results;

public sealed class PagedResultTests
{
    [Fact]
    public void Create_CalculatesPageMetadata()
    {
        var result = PagedResult<int>.Create(new[] { 21, 22 }, page: 3, pageSize: 10, totalItems: 22);

        Assert.Equal(3, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(22, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }
}
