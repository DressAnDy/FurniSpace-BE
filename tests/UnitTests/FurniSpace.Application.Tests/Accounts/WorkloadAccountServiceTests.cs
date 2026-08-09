#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.DTOs.Auth;
using FurniSpace.Application.Interfaces.Identity;
using FurniSpace.Application.Services.Accounts;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Accounts;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace FurniSpace.Application.Tests.Accounts;

public sealed class WorkloadAccountServiceTests
{
    static WorkloadAccountServiceTests()
    {
        MapsterTestSetup.EnsureConfigured();
    }

    [Fact]
    public async Task GetDesignerWorkloadAsync_ReturnsPagedItems()
    {
        var designerId = Guid.NewGuid();
        var repository = new FakeWorkloadAccountRepository
        {
            AvailableDesigners =
            [
                new AvailableDesignerReadModel
                {
                    AccountId = designerId,
                    Email = "d@example.com",
                    FullName = "Designer A",
                    Status = AccountStatus.ACTIVE,
                    DesignActiveCount = 1,
                    LifecycleAssignedCount = 2,
                    CurrentActiveProjectCount = 1,
                    MaxActiveProjects = 2,
                    AvailableSlot = 1,
                    CapacityState = "AVAILABLE"
                }
            ]
        };
        var service = CreateService(repository);

        var result = await service.GetDesignerWorkloadAsync(new DesignerWorkloadQueryDto
        {
            Page = 1,
            PageSize = 20,
            Search = " Designer ",
            CapacityState = "AVAILABLE",
            SortBy = "AvailableSlotDesc"
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal(designerId, item.AccountId);
        Assert.Equal("AVAILABLE", item.CapacityState);
        Assert.Equal(1, item.DesignActiveCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetDesignerWorkloadAsync_InvalidPage_ReturnsBadRequest(int page)
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetDesignerWorkloadAsync(new DesignerWorkloadQueryDto { Page = page });
        Assert.Equal(400, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetDesignerWorkloadAsync_InvalidPageSize_ReturnsBadRequest(int pageSize)
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetDesignerWorkloadAsync(new DesignerWorkloadQueryDto
        {
            Page = 1,
            PageSize = pageSize
        });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetDesignerWorkloadAsync_InvalidCapacityState_ReturnsBadRequest()
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetDesignerWorkloadAsync(new DesignerWorkloadQueryDto
        {
            Page = 1,
            CapacityState = "BUSY"
        });
        Assert.Equal(400, result.Status);
        Assert.Contains("AVAILABLE", result.Message);
    }

    [Fact]
    public async Task GetDesignerWorkloadSummaryAsync_ReturnsSummary()
    {
        var repository = new FakeWorkloadAccountRepository
        {
            AvailableDesigners =
            [
                new AvailableDesignerReadModel
                {
                    AccountId = Guid.NewGuid(),
                    Email = "a@example.com",
                    FullName = "A",
                    DesignActiveCount = 2,
                    CapacityState = "FULL"
                }
            ]
        };
        var service = CreateService(repository);

        var result = await service.GetDesignerWorkloadSummaryAsync();

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TotalActiveDesigners);
        Assert.Equal(1, result.Data.FullCount);
        Assert.Equal(2, result.Data.MaxActiveProjects);
    }

    [Fact]
    public async Task GetDesignerAssignedProjectsAsync_ReturnsMappedBuckets()
    {
        var designerId = Guid.NewGuid();
        var repository = new FakeWorkloadAccountRepository
        {
            IsActiveDesignerResult = true,
            DesignerAssignedProjects =
            [
                new DesignerAssignedProjectReadModel
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectCode = "PRJ-1",
                    ProjectName = "Cafe",
                    Status = ProjectStatus.PROPOSAL_CONSULTING,
                    DesignerAssignedAt = DateTime.UtcNow,
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Customer",
                    AssignedSalesId = Guid.NewGuid(),
                    SalesName = "Sales"
                }
            ]
        };
        var service = CreateService(repository);

        var result = await service.GetDesignerAssignedProjectsAsync(
            designerId,
            new DesignerAssignedProjectQueryDto { Page = 1, PageSize = 10, Bucket = "DESIGN_ACTIVE" });

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("DESIGN_ACTIVE", item.Bucket);
        Assert.Equal("PRJ-1", item.ProjectCode);
        Assert.Equal("Customer", item.CustomerName);
        Assert.Equal("Sales", item.SalesName);
    }

    [Fact]
    public async Task GetDesignerAssignedProjectsAsync_WhenNotDesigner_ReturnsNotFound()
    {
        var repository = new FakeWorkloadAccountRepository { IsActiveDesignerResult = false };
        var service = CreateService(repository);

        var result = await service.GetDesignerAssignedProjectsAsync(
            Guid.NewGuid(),
            new DesignerAssignedProjectQueryDto { Page = 1 });

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task GetDesignerAssignedProjectsAsync_InvalidBucket_ReturnsBadRequest()
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetDesignerAssignedProjectsAsync(
            Guid.NewGuid(),
            new DesignerAssignedProjectQueryDto { Page = 1, Bucket = "BAD" });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetSalesWorkloadAsync_MapsBreakdownAndAttentionCounters()
    {
        var salesId = Guid.NewGuid();
        var repository = new FakeWorkloadAccountRepository
        {
            SalesWorkloadItems =
            [
                new SalesWorkloadItemReadModel
                {
                    AccountId = salesId,
                    Email = "sales@example.com",
                    FullName = "Sales A",
                    Phone = "0901",
                    AvatarUrl = "https://cdn/a.png",
                    Status = AccountStatus.ACTIVE,
                    IntakeCount = 1,
                    CommercialCount = 1,
                    DesignMonitorCount = 3,
                    FulfillmentCount = 2,
                    SalesActiveCount = 2,
                    LifecycleAssignedCount = 7,
                    MaxActiveProjects = 5,
                    AvailableSlot = 3,
                    CapacityState = "AVAILABLE_NOW",
                    MeasurementRequiredCount = 0,
                    SpaceVerifiedCount = 1,
                    ProposalConsultingCount = 2,
                    InProductionCount = 1,
                    ProductionBlockedCount = 1,
                    ReadyForDeliveryCount = 1,
                    DeliveringCount = 0,
                    DeliveredCount = 1,
                    FuturePressureScore = 3.1m,
                    FuturePressureState = "HIGH",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            ]
        };
        var service = CreateService(repository);

        var result = await service.GetSalesWorkloadAsync(new SalesWorkloadQueryDto
        {
            Page = 1,
            PageSize = 20,
            Search = "Sales",
            CapacityState = "AVAILABLE_NOW",
            FuturePressureState = "HIGH",
            SortBy = "SalesActiveCountDesc"
        });

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(salesId, item.AccountId);
        Assert.Equal(2, item.ApproachingCommercialCount);
        Assert.Equal(1, item.ProductionAttentionCount);
        Assert.Equal(2, item.DeliveryAttentionCount);
        Assert.Equal(2, item.FuturePressureBreakdown.ProposalConsultingCount);
        Assert.Equal(3.1m, item.FuturePressureScore);
    }

    [Theory]
    [InlineData("BUSY")]
    [InlineData("AVAILABLE")]
    public async Task GetSalesWorkloadAsync_InvalidCapacity_ReturnsBadRequest(string capacity)
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetSalesWorkloadAsync(new SalesWorkloadQueryDto
        {
            Page = 1,
            CapacityState = capacity
        });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetSalesWorkloadAsync_InvalidPressure_ReturnsBadRequest()
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetSalesWorkloadAsync(new SalesWorkloadQueryDto
        {
            Page = 1,
            FuturePressureState = "CRITICAL"
        });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetSalesWorkloadSummaryAsync_ReturnsSummary()
    {
        var repository = new FakeWorkloadAccountRepository
        {
            SalesSummary = new SalesWorkloadSummaryReadModel
            {
                TotalActiveSales = 4,
                AvailableNowCount = 2,
                FullNowCount = 1,
                OverNowCount = 1,
                HighFuturePressureCount = 2,
                TotalSalesActiveProjects = 12,
                UnassignedIntakeCount = 3
            }
        };
        var service = CreateService(repository);

        var result = await service.GetSalesWorkloadSummaryAsync();

        Assert.Equal(200, result.Status);
        Assert.Equal(4, result.Data!.TotalActiveSales);
        Assert.Equal(3, result.Data.UnassignedIntakeCount);
        Assert.Equal(5, result.Data.MaxActiveProjects);
    }

    [Fact]
    public async Task GetSalesAssignedProjectsAsync_ReturnsMappedWeightAndBucket()
    {
        var salesId = Guid.NewGuid();
        var repository = new FakeWorkloadAccountRepository
        {
            IsActiveSalesResult = true,
            SalesAssignedProjects =
            [
                new SalesAssignedProjectReadModel
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectCode = "PRJ-S1",
                    ProjectName = "Office",
                    Status = ProjectStatus.PROPOSAL_CONSULTING,
                    SalesAssignedAt = DateTime.UtcNow,
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Cust",
                    AssignedDesignerId = Guid.NewGuid(),
                    DesignerName = "Design"
                }
            ]
        };
        var service = CreateService(repository);

        var result = await service.GetSalesAssignedProjectsAsync(
            salesId,
            new SalesAssignedProjectQueryDto { Page = 1, Bucket = "DESIGN_MONITOR" });

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("DESIGN_MONITOR", item.Bucket);
        Assert.Equal(1.00m, item.PressureWeight);
        Assert.Equal("Design", item.DesignerName);
    }

    [Fact]
    public async Task GetSalesAssignedProjectsAsync_WhenNotSales_ReturnsNotFound()
    {
        var repository = new FakeWorkloadAccountRepository { IsActiveSalesResult = false };
        var service = CreateService(repository);
        var result = await service.GetSalesAssignedProjectsAsync(
            Guid.NewGuid(),
            new SalesAssignedProjectQueryDto { Page = 1 });
        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task GetUnassignedIntakeProjectsAsync_ReturnsItems()
    {
        var repository = new FakeWorkloadAccountRepository
        {
            UnassignedIntakeProjects =
            [
                new UnassignedIntakeProjectReadModel
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectCode = "PRJ-U1",
                    ProjectName = "Lead",
                    BusinessType = "Cafe",
                    SubmittedAt = DateTime.UtcNow,
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Lead Customer"
                }
            ]
        };
        var service = CreateService(repository);

        var result = await service.GetUnassignedIntakeProjectsAsync(
            new UnassignedIntakeProjectQueryDto { Page = 1, PageSize = 10 });

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("PRJ-U1", item.ProjectCode);
        Assert.Equal("Cafe", item.BusinessType);
        Assert.Equal("Lead Customer", item.CustomerName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetUnassignedIntakeProjectsAsync_InvalidPageSize_ReturnsBadRequest(int pageSize)
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetUnassignedIntakeProjectsAsync(
            new UnassignedIntakeProjectQueryDto { Page = 1, PageSize = pageSize });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetDesignerAssignedProjectsAsync_EmptyDesignerId_ReturnsBadRequest()
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetDesignerAssignedProjectsAsync(
            Guid.Empty,
            new DesignerAssignedProjectQueryDto { Page = 1 });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetSalesAssignedProjectsAsync_EmptySalesId_ReturnsBadRequest()
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetSalesAssignedProjectsAsync(
            Guid.Empty,
            new SalesAssignedProjectQueryDto { Page = 1 });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetSalesAssignedProjectsAsync_InvalidBucket_ReturnsBadRequest()
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetSalesAssignedProjectsAsync(
            Guid.NewGuid(),
            new SalesAssignedProjectQueryDto { Page = 1, Bucket = "BAD" });
        Assert.Equal(400, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetSalesWorkloadAsync_InvalidPage_ReturnsBadRequest(int page)
    {
        var service = CreateService(new FakeWorkloadAccountRepository());
        var result = await service.GetSalesWorkloadAsync(new SalesWorkloadQueryDto { Page = page });
        Assert.Equal(400, result.Status);
    }

    private static AccountService CreateService(FakeWorkloadAccountRepository repository)
    {
        return new AccountService(
            repository,
            new FakeAuthService(),
            new InMemoryCacheService(),
            new NoOpSearchIndexService(),
            TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync),
            new PasswordHasher<Account>());
    }

    private sealed class FakeWorkloadAccountRepository : IAccountRepository
    {
        public IReadOnlyList<AvailableDesignerReadModel> AvailableDesigners { get; set; } = [];
        public IReadOnlyList<DesignerAssignedProjectReadModel> DesignerAssignedProjects { get; set; } = [];
        public IReadOnlyList<SalesWorkloadItemReadModel> SalesWorkloadItems { get; set; } = [];
        public IReadOnlyList<SalesAssignedProjectReadModel> SalesAssignedProjects { get; set; } = [];
        public IReadOnlyList<UnassignedIntakeProjectReadModel> UnassignedIntakeProjects { get; set; } = [];
        public SalesWorkloadSummaryReadModel SalesSummary { get; set; } = new();
        public bool IsActiveDesignerResult { get; set; } = true;
        public bool IsActiveSalesResult { get; set; } = true;

        public IQueryable<Account> Query() => Enumerable.Empty<Account>().AsQueryable();
        public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Account?>(null);
        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Account>>([]);
        public Task AddAsync(Account entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Account> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Account entity) { }
        public void Remove(Account entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult<Account?>(null);
        public Task<AccountDetailReadModel?> GetDetailAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<AccountDetailReadModel?>(null);
        public Task<string?> GetRoleNameAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<Guid?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<bool> RoleExistsAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> EmailExistsAsync(string email, Guid? excludedAccountId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<AvailableDesignerReadModel>> GetAvailableDesignersAsync(int page, int pageSize, int maxActiveProjects, string? search, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AvailableDesignerReadModel>>(AvailableDesigners);
        public Task<int> CountAvailableDesignersAsync(int maxActiveProjects, string? search, CancellationToken cancellationToken = default) =>
            Task.FromResult(AvailableDesigners.Count);
        public Task<IReadOnlyList<AvailableDesignerReadModel>> GetDesignerWorkloadAsync(int page, int pageSize, int maxActiveProjects, string? search, string? capacityState, string sortBy, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AvailableDesignerReadModel>>(AvailableDesigners);
        public Task<int> CountDesignerWorkloadAsync(int maxActiveProjects, string? search, string? capacityState, CancellationToken cancellationToken = default) =>
            Task.FromResult(AvailableDesigners.Count);
        public Task<DesignerWorkloadSummaryReadModel> GetDesignerWorkloadSummaryAsync(int maxActiveProjects, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DesignerWorkloadSummaryReadModel
            {
                TotalActiveDesigners = AvailableDesigners.Count,
                AvailableCount = AvailableDesigners.Count(d => d.CapacityState == "AVAILABLE"),
                FullCount = AvailableDesigners.Count(d => d.CapacityState == "FULL"),
                OverCount = AvailableDesigners.Count(d => d.CapacityState == "OVER"),
                TotalDesignActiveProjects = AvailableDesigners.Sum(d => d.DesignActiveCount)
            });
        public Task<IReadOnlyList<DesignerAssignedProjectReadModel>> GetDesignerAssignedProjectsAsync(Guid designerId, int page, int pageSize, string? bucket, CancellationToken cancellationToken = default) =>
            Task.FromResult(DesignerAssignedProjects);
        public Task<int> CountDesignerAssignedProjectsAsync(Guid designerId, string? bucket, CancellationToken cancellationToken = default) =>
            Task.FromResult(DesignerAssignedProjects.Count);
        public Task<bool> IsActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(IsActiveDesignerResult);
        public Task<IReadOnlyList<SalesWorkloadItemReadModel>> GetSalesWorkloadAsync(int page, int pageSize, int maxActiveProjects, string? search, string? capacityState, string? futurePressureState, string sortBy, CancellationToken cancellationToken = default) =>
            Task.FromResult(SalesWorkloadItems);
        public Task<int> CountSalesWorkloadAsync(int maxActiveProjects, string? search, string? capacityState, string? futurePressureState, CancellationToken cancellationToken = default) =>
            Task.FromResult(SalesWorkloadItems.Count);
        public Task<SalesWorkloadSummaryReadModel> GetSalesWorkloadSummaryAsync(int maxActiveProjects, CancellationToken cancellationToken = default) =>
            Task.FromResult(SalesSummary);
        public Task<IReadOnlyList<SalesAssignedProjectReadModel>> GetSalesAssignedProjectsAsync(Guid salesId, int page, int pageSize, string? bucket, CancellationToken cancellationToken = default) =>
            Task.FromResult(SalesAssignedProjects);
        public Task<int> CountSalesAssignedProjectsAsync(Guid salesId, string? bucket, CancellationToken cancellationToken = default) =>
            Task.FromResult(SalesAssignedProjects.Count);
        public Task<IReadOnlyList<UnassignedIntakeProjectReadModel>> GetUnassignedIntakeProjectsAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(UnassignedIntakeProjects);
        public Task<int> CountUnassignedIntakeProjectsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(UnassignedIntakeProjects.Count);
        public Task<bool> IsActiveSalesAsync(Guid salesId, CancellationToken cancellationToken = default) =>
            Task.FromResult(IsActiveSalesResult);
        public Task<IReadOnlyList<Account>> GetPagedAsync(int page, int pageSize, string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Account>>([]);
        public Task<int> CountAsync(string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<AccountFacetCountReadModel>> CountGroupedByStatusAsync(bool includeDeleted, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountFacetCountReadModel>>([]);
        public Task<IReadOnlyList<AccountFacetCountReadModel>> CountGroupedByRoleIdAsync(bool includeDeleted, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountFacetCountReadModel>>([]);
    }

    private sealed class FakeAuthService : IAuthService
    {
        public Task<AuthResponseDto> CreateSessionAsync(Guid userId, string email, string fullName, IEnumerable<string>? roles = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthResponseDto());
        public Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<AuthResponseDto?> RotateRefreshTokenAsync(Guid userId, string refreshToken, string email, string fullName, IEnumerable<string>? roles = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthResponseDto?>(null);
        public Task RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeUserAccessTokensAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsAccessTokenRevokedAsync(string jti, Guid userId, DateTimeOffset issuedAt, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
