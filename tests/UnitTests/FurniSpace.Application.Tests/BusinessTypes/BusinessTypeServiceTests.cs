#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.BusinessTypes;
using FurniSpace.Application.Services.BusinessTypes;
using FurniSpace.Application.Tests.TestDoubles;
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
        var service = CreateService(repository);

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
        var service = CreateService(repository);

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
        var service = CreateService(repository);

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
        var service = CreateService(repository);

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
        var service = CreateService(repository);

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
        var service = CreateService(repository);

        var result = await service.GetByIdAsync(businessTypeId);

        Assert.Equal(404, result.Status);
        Assert.Equal("BUSINESS_TYPE_NOT_FOUND", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_NormalizesCodeAndCreatesActiveBusinessType()
    {
        var repository = new FakeBusinessTypeRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(new CreateBusinessTypeRequestDto
        {
            Code = " cafe ",
            Name = " Quan ca phe ",
            Description = " Khong gian kinh doanh do uong. "
        });

        Assert.Equal(201, result.Status);
        Assert.Equal("Business Type created successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal("CAFE", result.Data.Code);
        Assert.Equal("Quan ca phe", result.Data.Name);
        Assert.Equal("Khong gian kinh doanh do uong.", result.Data.Description);
        Assert.True(result.Data.Status);
        Assert.NotEqual(default, result.Data.CreatedAt);
        Assert.Null(result.Data.UpdatedAt);
        Assert.Equal(1, repository.CodeExistsCallCount);
        Assert.Equal("CAFE", repository.CheckedCode);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithBlankDescription_StoresNullDescription()
    {
        var repository = new FakeBusinessTypeRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(new CreateBusinessTypeRequestDto
        {
            Code = "RETAIL",
            Name = "Retail",
            Description = " "
        });

        Assert.Equal(201, result.Status);
        Assert.Null(result.Data!.Description);
    }

    [Theory]
    [InlineData("", "Cafe", "Business type code is required.")]
    [InlineData(" ", "Cafe", "Business type code is required.")]
    [InlineData("CA-FE", "Cafe", "Business type code allows letters, numbers, and underscore only.")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZ", "Cafe", "Business type code must not exceed 50 characters.")]
    [InlineData("CAFE", "", "Business type name is required.")]
    [InlineData("CAFE", " ", "Business type name is required.")]
    public async Task CreateAsync_WithInvalidRequest_ReturnsBadRequest(
        string code,
        string name,
        string expectedError)
    {
        var repository = new FakeBusinessTypeRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(new CreateBusinessTypeRequestDto
        {
            Code = code,
            Name = name
        });

        Assert.Equal(400, result.Status);
        Assert.Contains(expectedError, result.Errors!);
        Assert.Equal(0, repository.CodeExistsCallCount);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithTooLongName_ReturnsBadRequest()
    {
        var repository = new FakeBusinessTypeRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(new CreateBusinessTypeRequestDto
        {
            Code = "CAFE",
            Name = new string('A', 151)
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Business type name must not exceed 150 characters.", result.Errors!);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateCode_ReturnsConflict()
    {
        var repository = new FakeBusinessTypeRepository { CodeExists = true };
        var service = CreateService(repository);

        var result = await service.CreateAsync(new CreateBusinessTypeRequestDto
        {
            Code = "cafe",
            Name = "Cafe"
        });

        Assert.Equal(409, result.Status);
        Assert.Equal("BUSINESS_TYPE_CODE_ALREADY_EXISTS", result.Message);
        Assert.Null(result.Data);
        Assert.Equal("CAFE", repository.CheckedCode);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingBusinessType_UpdatesDisplayFields()
    {
        var updatedAt = new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc);
        var repository = new FakeBusinessTypeRepository
        {
            Detail = new BusinessType
            {
                Id = 1,
                Code = "CAFE",
                Name = "Old Cafe",
                Description = "Old",
                Status = true,
                CreatedAt = updatedAt.AddDays(-1)
            }
        };
        var service = CreateService(repository);

        var result = await service.UpdateAsync(1, new UpdateBusinessTypeRequestDto
        {
            Name = " Cafe / Coffee Shop ",
            Description = " Updated terminology. "
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Business Type updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Id);
        Assert.Equal("CAFE", result.Data.Code);
        Assert.Equal("Cafe / Coffee Shop", result.Data.Name);
        Assert.Equal("Updated terminology.", result.Data.Description);
        Assert.NotNull(result.Data.UpdatedAt);
        Assert.Equal(1, repository.GetForUpdateCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithBlankDescription_StoresNullDescription()
    {
        var repository = new FakeBusinessTypeRepository
        {
            Detail = new BusinessType { Id = 1, Code = "CAFE", Name = "Cafe", Status = true }
        };
        var service = CreateService(repository);

        var result = await service.UpdateAsync(1, new UpdateBusinessTypeRequestDto
        {
            Name = "Cafe",
            Description = " "
        });

        Assert.Equal(200, result.Status);
        Assert.Null(result.Data!.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task UpdateAsync_WithMissingBusinessType_ReturnsNotFound(int businessTypeId)
    {
        var repository = new FakeBusinessTypeRepository();
        var service = CreateService(repository);

        var result = await service.UpdateAsync(businessTypeId, new UpdateBusinessTypeRequestDto
        {
            Name = "Cafe"
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("BUSINESS_TYPE_NOT_FOUND", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(businessTypeId > 0 ? 1 : 0, repository.GetForUpdateCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("", "Business type name is required.")]
    [InlineData(" ", "Business type name is required.")]
    public async Task UpdateAsync_WithInvalidName_ReturnsBadRequest(string name, string expectedError)
    {
        var repository = new FakeBusinessTypeRepository();
        var service = CreateService(repository);

        var result = await service.UpdateAsync(1, new UpdateBusinessTypeRequestDto
        {
            Name = name
        });

        Assert.Equal(400, result.Status);
        Assert.Contains(expectedError, result.Errors!);
        Assert.Equal(0, repository.GetForUpdateCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithTooLongName_ReturnsBadRequest()
    {
        var repository = new FakeBusinessTypeRepository();
        var service = CreateService(repository);

        var result = await service.UpdateAsync(1, new UpdateBusinessTypeRequestDto
        {
            Name = new string('A', 151)
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Business type name must not exceed 150 characters.", result.Errors!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateStatusAsync_WithExistingBusinessType_UpdatesStatusIdempotently(bool requestedStatus)
    {
        var repository = new FakeBusinessTypeRepository
        {
            Detail = new BusinessType
            {
                Id = 1,
                Code = "CAFE",
                Name = "Cafe",
                Status = requestedStatus,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };
        var service = CreateService(repository);

        var result = await service.UpdateStatusAsync(1, new UpdateBusinessTypeStatusRequestDto
        {
            Status = requestedStatus
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Business Type status updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(requestedStatus, result.Data.Status);
        Assert.NotNull(result.Data.UpdatedAt);
        Assert.Equal(1, repository.GetForUpdateCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task UpdateStatusAsync_WithMissingBusinessType_ReturnsNotFound(int businessTypeId)
    {
        var repository = new FakeBusinessTypeRepository();
        var service = CreateService(repository);

        var result = await service.UpdateStatusAsync(businessTypeId, new UpdateBusinessTypeStatusRequestDto
        {
            Status = false
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("BUSINESS_TYPE_NOT_FOUND", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(businessTypeId > 0 ? 1 : 0, repository.GetForUpdateCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    private static BusinessTypeService CreateService(FakeBusinessTypeRepository repository)
    {
        return new BusinessTypeService(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));
    }

    private sealed class FakeBusinessTypeRepository : IBusinessTypeRepository
    {
        public IReadOnlyList<BusinessType> Items { get; set; } = [];
        public BusinessType? Detail { get; set; }
        public int Total { get; set; }
        public bool CodeExists { get; set; }
        public string? CheckedCode { get; private set; }
        public int DetailId { get; private set; }
        public bool Status { get; private set; }
        public string? Keyword { get; private set; }
        public int Page { get; private set; }
        public int Limit { get; private set; }
        public int AddCallCount { get; private set; }
        public int CodeExistsCallCount { get; private set; }
        public int GetForUpdateCallCount { get; private set; }
        public int GetPagedCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }

        public Task AddAsync(BusinessType businessType, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            Detail = businessType;
            return Task.CompletedTask;
        }

        public Task<BusinessType?> GetByIdAsync(
            int businessTypeId,
            CancellationToken cancellationToken = default)
        {
            DetailId = businessTypeId;
            return Task.FromResult(Detail?.Id == businessTypeId ? Detail : null);
        }

        public Task<BusinessType?> GetForUpdateAsync(
            int businessTypeId,
            CancellationToken cancellationToken = default)
        {
            GetForUpdateCallCount++;
            return Task.FromResult(Detail?.Id == businessTypeId ? Detail : null);
        }

        public Task<bool> CodeExistsAsync(
            string normalizedCode,
            CancellationToken cancellationToken = default)
        {
            CodeExistsCallCount++;
            CheckedCode = normalizedCode;
            return Task.FromResult(CodeExists);
        }

        public Task<IReadOnlyList<BusinessType>> GetByIdsAsync(
            IReadOnlyCollection<int> businessTypeIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BusinessType>>([]);
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

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}
