#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Catalog;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.BusinessTypes;
using FurniSpace.Application.Interfaces.BusinessTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class BusinessTypesControllerTests
{
    [Theory]
    [InlineData(nameof(BusinessTypesController.GetAll))]
    [InlineData(nameof(BusinessTypesController.GetById))]
    public void ReadActions_DoNotRequireAuthorization(string methodName)
    {
        var method = typeof(BusinessTypesController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == methodName);

        var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.Null(authorize);
    }

    [Fact]
    public async Task GetAll_ReturnsServiceResultThroughBaseController()
    {
        var response = new BusinessTypeListResponseDto
        {
            Page = 1,
            Limit = 20,
            Total = 1,
            Items = [new BusinessTypeDto { Id = 1, Code = "CAFE", Name = "Quan ca phe", Status = true }]
        };
        var service = new FakeBusinessTypeService(
            ServiceResult<BusinessTypeListResponseDto>.Success(response, "Business Types retrieved successfully."),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto(), "Business Type retrieved successfully."));
        var controller = new BusinessTypesController(service);
        var query = new BusinessTypeQueryDto { Keyword = "cafe", Page = 1, Limit = 20 };

        var actionResult = await controller.GetAll(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<BusinessTypeListResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Same(query, service.Query);
    }

    [Fact]
    public async Task GetById_ReturnsServiceResultThroughBaseController()
    {
        var response = new BusinessTypeDto { Id = 1, Code = "CAFE", Name = "Quan ca phe", Status = true };
        var service = new FakeBusinessTypeService(
            ServiceResult<BusinessTypeListResponseDto>.Success(new BusinessTypeListResponseDto()),
            ServiceResult<BusinessTypeDto>.Success(response, "Business Type retrieved successfully."));
        var controller = new BusinessTypesController(service);

        var actionResult = await controller.GetById(1);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<BusinessTypeDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(1, service.BusinessTypeId);
    }

    private sealed class FakeBusinessTypeService : IBusinessTypeService
    {
        private readonly ServiceResult<BusinessTypeListResponseDto> _getAllResult;
        private readonly ServiceResult<BusinessTypeDto> _getByIdResult;

        public FakeBusinessTypeService(
            ServiceResult<BusinessTypeListResponseDto> getAllResult,
            ServiceResult<BusinessTypeDto> getByIdResult)
        {
            _getAllResult = getAllResult;
            _getByIdResult = getByIdResult;
        }

        public BusinessTypeQueryDto? Query { get; private set; }
        public int BusinessTypeId { get; private set; }

        public Task<ServiceResult<BusinessTypeListResponseDto>> GetAllAsync(
            BusinessTypeQueryDto query,
            CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult(_getAllResult);
        }

        public Task<ServiceResult<BusinessTypeDto>> GetByIdAsync(
            int businessTypeId,
            CancellationToken cancellationToken = default)
        {
            BusinessTypeId = businessTypeId;
            return Task.FromResult(_getByIdResult);
        }
    }
}
