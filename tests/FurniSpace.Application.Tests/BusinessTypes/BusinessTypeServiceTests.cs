#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.BusinessTypes;
using FurniSpace.Application.Services.BusinessTypes;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.BusinessTypes;

public sealed class BusinessTypeServiceTests
{
    static BusinessTypeServiceTests()
    {
        MapsterTestSetup.EnsureConfigured();
    }

    [Fact]
    public async Task GetAllAsync_WithDefaultStatus_ReturnsActiveBusinessTypes()
    {
        var createdAt = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
        var repository = new FakeBusinessTypeRepository
        {
            Items =
            [
                new BusinessType
                {
                    Id = 1,
                    Code = "CAFE",
                    Name = "Quan ca phe",
                    Description = "Khong gian kinh doanh do uong.",
                    Status = true,
                    CreatedAt = createdAt
                }
            ],
            Total = 1
        };
        var service = new BusinessTypeService(repository);

        var result = await service.GetAllAsync(new BusinessTypeQueryDto
        {
            Keyword = " cafe ",
            Page = 1,
            Limit = 20
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Business Types retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.True(repository.Status);
        Assert.Equal("cafe", repository.Keyword);
        Assert.Equal(1, repository.Page);
        Assert.Equal(20, repository.Limit);
        Assert.Equal(1, result.Data.Total);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal(1, item.Id);
        Assert.Equal("CAFE", item.Code);
        Assert.Equal("Quan ca phe", item.Name);
        Assert.Equal(createdAt, item.CreatedAt);
    }

    [Fact]
    public async Task GetAllAsync_WithInactiveStatus_PassesFalseFilterToRepository()
    {
        var repository = new FakeBusinessTypeRepository();
        var service = new BusinessTypeService(repository);

        var result = await service.GetAllAsync(new BusinessTypeQueryDto
        {
            Status = false,
            Page = 2,
            Limit = 10
        });

        Assert.Equal(200, result.Status);
        Assert.False(repository.Status);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(10, result.Data.Limit);
    }

    [Theory]
    [InlineData(0, 20, "Page must be greater than zero.")]
    [InlineData(1, 0, "Limit must be between 1 and 100.")]
    [InlineData(1, 101, "Limit must be between 1 and 100.")]
    public async Task GetAllAsync_WithInvalidPagination_ReturnsBadRequest(
        int page,
        int limit,
        string expectedError)
    {
        var repository = new FakeBusinessTypeRepository();
        var service = new BusinessTypeService(repository);

        var result = await service.GetAllAsync(new BusinessTypeQueryDto
        {
            Page = page,
            Limit = limit
        });

        Assert.Equal(400, result.Status);
        Assert.Contains(expectedError, result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetPagedCallCount);
    }

    [Fact]
    public async Task GetAllAsync_WithTooLongKeyword_ReturnsBadRequest()
    {
        var repository = new FakeBusinessTypeRepository();
        var service = new BusinessTypeService(repository);

        var result = await service.GetAllAsync(new BusinessTypeQueryDto
        {
            Keyword = new string('A', 101)
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Keyword must not exceed 100 characters.", result.Errors!);
        Assert.Equal(0, repository.GetPagedCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingBusinessType_ReturnsDetail()
    {
        var repository = new FakeBusinessTypeRepository
        {
            Detail = new BusinessType
            {
                Id = 2,
                Code = "SPA",
                Name = "Spa",
                Status = false,
                CreatedAt = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc)
            }
        };
        var service = new BusinessTypeService(repository);

        var result = await service.GetByIdAsync(2);

        Assert.Equal(200, result.Status);
        Assert.Equal("Business Type retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Id);
        Assert.Equal("SPA", result.Data.Code);
        Assert.False(result.Data.Status);
        Assert.Equal(2, repository.DetailId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task GetByIdAsync_WithMissingBusinessType_ReturnsNotFound(int businessTypeId)
    {
        var repository = new FakeBusinessTypeRepository();
        var service = new BusinessTypeService(repository);

        var result = await service.GetByIdAsync(businessTypeId);

        Assert.Equal(404, result.Status);
        Assert.Equal("BUSINESS_TYPE_NOT_FOUND", result.Message);
        Assert.Null(result.Data);
    }

    private sealed class FakeBusinessTypeRepository : IBusinessTypeRepository
    {
        public IReadOnlyList<BusinessType> Items { get; set; } = [];
        public BusinessType? Detail { get; set; }
        public int Total { get; set; }
        public int DetailId { get; private set; }
        public bool Status { get; private set; }
        public string? Keyword { get; private set; }
        public int Page { get; private set; }
        public int Limit { get; private set; }
        public int GetPagedCallCount { get; private set; }

        public Task<BusinessType?> GetByIdAsync(
            int businessTypeId,
            CancellationToken cancellationToken = default)
        {
            DetailId = businessTypeId;
            return Task.FromResult(Detail?.Id == businessTypeId ? Detail : null);
        }

        public Task<IReadOnlyList<BusinessType>> GetPagedAsync(
            bool status,
            string? keyword,
            int page,
            int limit,
            CancellationToken cancellationToken = default)
        {
            GetPagedCallCount++;
            Status = status;
            Keyword = keyword;
            Page = page;
            Limit = limit;
            return Task.FromResult(Items);
        }

        public Task<int> CountAsync(
            bool status,
            string? keyword,
            CancellationToken cancellationToken = default)
        {
            Status = status;
            Keyword = keyword;
            return Task.FromResult(Total);
        }
    }
}
