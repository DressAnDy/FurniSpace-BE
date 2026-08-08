#nullable enable

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Admin;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.Interfaces.Catalog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class AdminCatalogControllerTests
{
    [Fact]
    public async Task GetProducts_ReturnsServiceResultThroughBaseController()
    {
        var response = new AdminCatalogListResponseDto
        {
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };
        var service = new FakeAdminCatalogService(
            ServiceResult<AdminCatalogListResponseDto>.Success(response, string.Empty));
        var controller = new AdminCatalogController(service);

        var actionResult = await controller.GetProducts(new AdminCatalogQueryDto
        {
            Page = 1,
            PageSize = 20
        });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
    }

    private sealed class FakeAdminCatalogService : IAdminCatalogService
    {
        private readonly ServiceResult<AdminCatalogListResponseDto> _result;

        public FakeAdminCatalogService(ServiceResult<AdminCatalogListResponseDto> result)
        {
            _result = result;
        }

        public Task<ServiceResult<AdminCatalogListResponseDto>> GetProductsAsync(
            AdminCatalogQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }
}
