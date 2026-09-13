#nullable enable

using FurniSpace.Application.Common;
using Xunit;

namespace FurniSpace.Application.Tests.Common.Results;

public sealed class ErrorTests
{
    [Fact]
    public void Conflict_SetsCodeMessageAndStatus()
    {
        var error = Error.Conflict("Product.SkuExists", "SKU already exists");

        Assert.Equal("Product.SkuExists", error.Code);
        Assert.Equal("SKU already exists", error.Message);
        Assert.Equal(409, error.Status);
    }
}
