#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Dashboard;
using FurniSpace.Application.Services.Dashboard;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Dashboard;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Dashboard;

public sealed class DashboardQueueServiceTests
{
    [Fact]
    public async Task GetSalesActionQueueAsync_MapsItemsAndCountsByGroup()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var dashboard = new FakeDashboardQueueReadRepository
        {
            SalesRows =
            [
                new DashboardProjectQueueRowReadModel
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-001",
                    ProjectName = "Showroom A",
                    Status = ProjectStatus.SUBMITTED,
                    CustomerName = "Customer A",
                    AssignedSalesName = "Sales One",
                    UpdatedAt = DateTime.UtcNow
                }
            ]
        };
        var projects = new FakeProjectRepository { RoleName = "SALES" };
        var service = new DashboardQueueService(dashboard, projects);

        var result = await service.GetSalesActionQueueAsync(
            userId,
            new DashboardQueueQueryDto { Scope = "mine", Page = 1, Limit = 20 });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("Review request", result.Data.Items[0].Action);
        Assert.Equal("Customer A", result.Data.Items[0].CustomerName);
        Assert.True(result.Data.CountsByGroup.ContainsKey("Intake"));
        Assert.Equal(1, result.Data.CountsByGroup["Intake"]);
        Assert.Equal(1, result.Data.Total);
    }

    [Fact]
    public async Task GetSalesActionQueueAsync_WhenForbiddenRole_ReturnsForbidden()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = "CUSTOMER" });

        var result = await service.GetSalesActionQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetSalesKpisAsync_ReturnsAggregates()
    {
        var dashboard = new FakeDashboardQueueReadRepository
        {
            SalesKpis = new SalesDashboardKpisReadModel
            {
                NewRequests = 2,
                WaitingCustomer = 3,
                PaymentFollowUp = 1,
                OverdueTasks = 4,
                ActiveProjects = 8
            }
        };
        var service = new DashboardQueueService(dashboard, new FakeProjectRepository { RoleName = "SALES" });

        var result = await service.GetSalesKpisAsync(Guid.NewGuid(), new DashboardQueueQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.Data!.NewRequests);
        Assert.Equal(3, result.Data.WaitingCustomer);
        Assert.Equal(1, result.Data.PaymentFollowUp);
    }

    [Fact]
    public async Task GetDesignerWorkQueueAsync_FiltersByGroup()
    {
        var dashboard = new FakeDashboardQueueReadRepository
        {
            DesignerRows =
            [
                new DashboardProjectQueueRowReadModel
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectName = "Measure me",
                    Status = ProjectStatus.MEASUREMENT_REQUIRED,
                    CustomerName = "C1",
                    AssignedDesignerName = "D1",
                    UpdatedAt = DateTime.UtcNow
                },
                new DashboardProjectQueueRowReadModel
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectName = "Proposal me",
                    Status = ProjectStatus.PROPOSAL_CONSULTING,
                    CustomerName = "C2",
                    AssignedDesignerName = "D1",
                    UpdatedAt = DateTime.UtcNow
                }
            ]
        };
        var service = new DashboardQueueService(dashboard, new FakeProjectRepository { RoleName = "DESIGNER" });

        var result = await service.GetDesignerWorkQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto { Group = "Design", Page = 1, Limit = 20 });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Complete measurement", result.Data.Items[0].Action);
        Assert.Equal(1, result.Data.Total);
    }

    [Fact]
    public async Task GetProductionQueueAsync_MapsProductionItems()
    {
        var productionRequestId = Guid.NewGuid();
        var dashboard = new FakeDashboardQueueReadRepository
        {
            ProductionRows =
            [
                new DashboardProductionQueueRowReadModel
                {
                    ProductionRequestId = productionRequestId,
                    ProductionCode = "PR-1",
                    ProjectId = Guid.NewGuid(),
                    ProjectName = "Factory job",
                    CustomerName = "Buyer",
                    AssignedToName = "Prod Staff",
                    Status = ProductionRequestStatus.PENDING_REVIEW,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        var service = new DashboardQueueService(dashboard, new FakeProjectRepository { RoleName = "PRODUCTION" });

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto { Scope = "team" });

        Assert.Equal(200, result.Status);
        Assert.Equal(productionRequestId.ToString("D"), result.Data!.Items[0].Id);
        Assert.Equal("Review production request", result.Data.Items[0].Action);
    }

    private sealed class FakeDashboardQueueReadRepository : IDashboardQueueReadRepository
    {
        public IReadOnlyList<DashboardProjectQueueRowReadModel> SalesRows { get; init; } = [];
        public IReadOnlyList<DashboardProjectQueueRowReadModel> DesignerRows { get; init; } = [];
        public IReadOnlyList<DashboardProductionQueueRowReadModel> ProductionRows { get; init; } = [];
        public SalesDashboardKpisReadModel SalesKpis { get; init; } = new();
        public DesignerDashboardKpisReadModel DesignerKpis { get; init; } = new();
        public ProductionDashboardKpisReadModel ProductionKpis { get; init; } = new();

        public Task<IReadOnlyList<DashboardProjectQueueRowReadModel>> GetSalesQueueRowsAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SalesRows);

        public Task<SalesDashboardKpisReadModel> GetSalesKpisAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SalesKpis);

        public Task<IReadOnlyList<DashboardProjectQueueRowReadModel>> GetDesignerQueueRowsAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DesignerRows);

        public Task<DesignerDashboardKpisReadModel> GetDesignerKpisAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DesignerKpis);

        public Task<IReadOnlyList<DashboardProductionQueueRowReadModel>> GetProductionQueueRowsAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductionRows);

        public Task<ProductionDashboardKpisReadModel> GetProductionKpisAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductionKpis);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        public string? RoleName { get; init; }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RoleName);

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectDetailReadModel?>(null);

        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DesignerAccountReadModel?>(null);

        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
            ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);

        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);

        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid projectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectSearchIndexItemReadModel?>(null);

        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);

        public IQueryable<Project> Query() => Array.Empty<Project>().AsQueryable();

        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Project?>(null);

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Project>>([]);

        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Update(Project entity)
        {
        }

        public void Remove(Project entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
