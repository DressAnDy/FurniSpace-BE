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
    [InlineData(nameof(BusinessTypesController.Create))]
    [InlineData(nameof(BusinessTypesController.Update))]
    [InlineData(nameof(BusinessTypesController.UpdateStatus))]
    public void WriteActions_RequireAdminAuthorization(string methodName)
    {
        var method = typeof(BusinessTypesController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == methodName);

        var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("ADMIN", authorize.Roles);
    }

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
            ServiceResult<BusinessTypeDto>.Created(new BusinessTypeDto()),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()),
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
            ServiceResult<BusinessTypeDto>.Created(new BusinessTypeDto()),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()),
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

    [Fact]
    public async Task Create_ReturnsCreatedServiceResultThroughBaseController()
    {
        var response = new BusinessTypeDto { Id = 1, Code = "CAFE", Name = "Cafe", Status = true };
        var service = new FakeBusinessTypeService(
            ServiceResult<BusinessTypeDto>.Created(response, "Business Type created successfully."),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()),
            ServiceResult<BusinessTypeListResponseDto>.Success(new BusinessTypeListResponseDto()),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()));
        var controller = new BusinessTypesController(service);
        var request = new CreateBusinessTypeRequestDto { Code = "CAFE", Name = "Cafe" };

        var actionResult = await controller.Create(request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<BusinessTypeDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Same(request, service.CreateRequest);
    }

    [Fact]
    public async Task Update_ReturnsUpdatedServiceResultThroughBaseController()
    {
        var response = new BusinessTypeDto { Id = 1, Code = "CAFE", Name = "Cafe", Status = true };
        var service = new FakeBusinessTypeService(
            ServiceResult<BusinessTypeDto>.Created(new BusinessTypeDto()),
            ServiceResult<BusinessTypeDto>.Success(response, "Business Type updated successfully."),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()),
            ServiceResult<BusinessTypeListResponseDto>.Success(new BusinessTypeListResponseDto()),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()));
        var controller = new BusinessTypesController(service);
        var request = new UpdateBusinessTypeRequestDto { Name = "Cafe" };

        var actionResult = await controller.Update(1, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<BusinessTypeDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(1, service.BusinessTypeId);
        Assert.Same(request, service.UpdateRequest);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsUpdatedStatusServiceResultThroughBaseController()
    {
        var response = new BusinessTypeDto { Id = 1, Code = "CAFE", Name = "Cafe", Status = false };
        var service = new FakeBusinessTypeService(
            ServiceResult<BusinessTypeDto>.Created(new BusinessTypeDto()),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()),
            ServiceResult<BusinessTypeDto>.Success(response, "Business Type status updated successfully."),
            ServiceResult<BusinessTypeListResponseDto>.Success(new BusinessTypeListResponseDto()),
            ServiceResult<BusinessTypeDto>.Success(new BusinessTypeDto()));
        var controller = new BusinessTypesController(service);
        var request = new UpdateBusinessTypeStatusRequestDto { Status = false };

        var actionResult = await controller.UpdateStatus(1, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<BusinessTypeDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(1, service.BusinessTypeId);
        Assert.Same(request, service.UpdateStatusRequest);
    }

    private sealed class FakeBusinessTypeService : IBusinessTypeService
    {
        private readonly ServiceResult<BusinessTypeDto> _createResult;
        private readonly ServiceResult<BusinessTypeDto> _updateResult;
        private readonly ServiceResult<BusinessTypeDto> _updateStatusResult;
        private readonly ServiceResult<BusinessTypeListResponseDto> _getAllResult;
        private readonly ServiceResult<BusinessTypeDto> _getByIdResult;

        public FakeBusinessTypeService(
            ServiceResult<BusinessTypeDto> createResult,
            ServiceResult<BusinessTypeDto> updateResult,
            ServiceResult<BusinessTypeDto> updateStatusResult,
            ServiceResult<BusinessTypeListResponseDto> getAllResult,
            ServiceResult<BusinessTypeDto> getByIdResult)
        {
            _createResult = createResult;
            _updateResult = updateResult;
            _updateStatusResult = updateStatusResult;
            _getAllResult = getAllResult;
            _getByIdResult = getByIdResult;
        }

        public CreateBusinessTypeRequestDto? CreateRequest { get; private set; }
        public UpdateBusinessTypeRequestDto? UpdateRequest { get; private set; }
        public UpdateBusinessTypeStatusRequestDto? UpdateStatusRequest { get; private set; }
        public BusinessTypeQueryDto? Query { get; private set; }
        public int BusinessTypeId { get; private set; }

        public Task<ServiceResult<BusinessTypeDto>> CreateAsync(
            CreateBusinessTypeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CreateRequest = request;
            return Task.FromResult(_createResult);
        }

        public Task<ServiceResult<BusinessTypeDto>> UpdateAsync(
            int businessTypeId,
            UpdateBusinessTypeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            BusinessTypeId = businessTypeId;
            UpdateRequest = request;
            return Task.FromResult(_updateResult);
        }

        public Task<ServiceResult<BusinessTypeDto>> UpdateStatusAsync(
            int businessTypeId,
            UpdateBusinessTypeStatusRequestDto request,
            CancellationToken cancellationToken = default)
        {
            BusinessTypeId = businessTypeId;
            UpdateStatusRequest = request;
            return Task.FromResult(_updateStatusResult);
        }

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
