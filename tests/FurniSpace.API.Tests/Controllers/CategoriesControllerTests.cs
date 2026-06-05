#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Categories;
using FurniSpace.Application.Interfaces.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class CategoriesControllerTests
{
    [Fact]
    public void Create_RequiresAdminRole()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(CategoriesController.Create));

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void Update_RequiresAdminRole()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(CategoriesController.Update));

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void GetAll_DoesNotRequireAuthorization()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(CategoriesController.GetAll));

        Assert.Null(authorize);
    }

    [Fact]
    public async Task Create_ReturnsCreatedServiceResultThroughBaseController()
    {
        var response = new CategoryDto
        {
            CategoryId = Guid.NewGuid(),
            CategoryName = "Lighting",
            Description = "Lighting and decorative lighting items",
            Status = "ACTIVE"
        };
        var service = new FakeCategoryService(
            createResult: ServiceResult<CategoryDto>.Created(response, "Category created successfully."),
            updateResult: ServiceResult<CategoryDto>.Success(new CategoryDto(), "Category updated successfully."),
            getAllResult: ServiceResult<CategoryListResponseDto>.Success(new CategoryListResponseDto(), string.Empty));
        var controller = new CategoriesController(service);
        var request = new CreateCategoryRequestDto
        {
            CategoryName = "Lighting",
            Description = "Lighting and decorative lighting items"
        };

        var actionResult = await controller.Create(request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<CategoryDto>>(objectResult.Value);
        Assert.Equal(201, result.Status);
        Assert.Equal("Category created successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Same(request, service.CreateRequest);
    }

    [Fact]
    public async Task Update_ReturnsServiceResultThroughBaseController()
    {
        var categoryId = Guid.NewGuid();
        var response = new CategoryDto
        {
            CategoryId = categoryId,
            CategoryName = "Lighting",
            Description = "Lighting and decorative lighting items",
            Status = "ACTIVE"
        };
        var service = new FakeCategoryService(
            createResult: ServiceResult<CategoryDto>.Created(new CategoryDto(), "Category created successfully."),
            updateResult: ServiceResult<CategoryDto>.Success(response, "Category updated successfully."),
            getAllResult: ServiceResult<CategoryListResponseDto>.Success(new CategoryListResponseDto(), string.Empty));
        var controller = new CategoriesController(service);
        var request = new UpdateCategoryRequestDto
        {
            CategoryName = "Lighting",
            Description = "Lighting and decorative lighting items"
        };

        var actionResult = await controller.Update(categoryId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<CategoryDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Category updated successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(categoryId, service.UpdateCategoryId);
        Assert.Same(request, service.UpdateRequest);
    }

    [Fact]
    public async Task GetAll_ReturnsServiceResultThroughBaseController()
    {
        var response = new CategoryListResponseDto
        {
            Page = 1,
            Limit = 20,
            Total = 1,
            Items =
            [
                new CategoryDto
                {
                    CategoryId = Guid.NewGuid(),
                    CategoryName = "Counter",
                    Description = "Counter and cashier furniture",
                    Status = "ACTIVE"
                }
            ]
        };
        var service = new FakeCategoryService(
            createResult: ServiceResult<CategoryDto>.Created(new CategoryDto(), "Category created successfully."),
            updateResult: ServiceResult<CategoryDto>.Success(new CategoryDto(), "Category updated successfully."),
            getAllResult: ServiceResult<CategoryListResponseDto>.Success(response, string.Empty));
        var controller = new CategoriesController(service);

        var actionResult = await controller.GetAll(page: 1, limit: 20);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<CategoryListResponseDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal(string.Empty, result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(1, service.Page);
        Assert.Equal(20, service.Limit);
    }

    private sealed class FakeCategoryService : ICategoryService
    {
        private readonly ServiceResult<CategoryDto> _createResult;
        private readonly ServiceResult<CategoryDto> _updateResult;
        private readonly ServiceResult<CategoryListResponseDto> _getAllResult;

        public FakeCategoryService(
            ServiceResult<CategoryDto> createResult,
            ServiceResult<CategoryDto> updateResult,
            ServiceResult<CategoryListResponseDto> getAllResult)
        {
            _createResult = createResult;
            _updateResult = updateResult;
            _getAllResult = getAllResult;
        }

        public CreateCategoryRequestDto? CreateRequest { get; private set; }
        public Guid UpdateCategoryId { get; private set; }
        public UpdateCategoryRequestDto? UpdateRequest { get; private set; }
        public int Page { get; private set; }
        public int Limit { get; private set; }

        public Task<ServiceResult<CategoryDto>> CreateAsync(
            CreateCategoryRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CreateRequest = request;
            return Task.FromResult(_createResult);
        }

        public Task<ServiceResult<CategoryDto>> UpdateAsync(
            Guid categoryId,
            UpdateCategoryRequestDto request,
            CancellationToken cancellationToken = default)
        {
            UpdateCategoryId = categoryId;
            UpdateRequest = request;
            return Task.FromResult(_updateResult);
        }

        public Task<ServiceResult<CategoryListResponseDto>> GetAllAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
        {
            Page = page;
            Limit = limit;
            return Task.FromResult(_getAllResult);
        }
    }

    private static AuthorizeAttribute? GetMethodAuthorizeAttribute(string methodName)
    {
        var method = typeof(CategoriesController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == methodName);

        return method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
    }
}
