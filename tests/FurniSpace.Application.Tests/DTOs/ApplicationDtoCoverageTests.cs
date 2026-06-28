#nullable enable

using System;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.DTOs.Products;
using Xunit;

namespace FurniSpace.Application.Tests.DTOs;

public sealed class ApplicationDtoCoverageTests
{
    [Fact]
    public void AccountSuggestResponseDto_StoresItems()
    {
        var accountId = Guid.NewGuid();
        var response = new AccountSuggestResponseDto
        {
            Items =
            [
                new AccountSuggestItemDto
                {
                    AccountId = accountId,
                    FullName = "Nguyen Van A",
                    Email = "a@example.com"
                }
            ]
        };

        Assert.Single(response.Items);
        Assert.Equal(accountId, response.Items[0].AccountId);
        Assert.Equal("Nguyen Van A", response.Items[0].FullName);
        Assert.Equal("a@example.com", response.Items[0].Email);
    }

    [Fact]
    public void ProductListResponseDto_StoresPagingAndDefaultsItems()
    {
        var response = new ProductListResponseDto
        {
            Page = 2,
            Limit = 20,
            Total = 41,
            Facets = new ProductSearchFacetsDto()
        };

        Assert.Empty(response.Items);
        Assert.Equal(2, response.Page);
        Assert.Equal(20, response.Limit);
        Assert.Equal(41, response.Total);
        Assert.NotNull(response.Facets);
    }
}
