#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.OperationalDelayReports;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Application.Services.OperationalDelayReports;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.OperationalDelayReports;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.OperationalDelayReports;

public sealed class OperationalDelayReportServiceTests
{
    private const string SalesRole = "SALES";
    private const string ProductionRole = "PRODUCTION";
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";

    [Fact]
    public async Task CreateProductionReportAsync_WithAssignedSales_CreatesReport()
    {
        var ids = CreateIds();
        var reports = new FakeOperationalDelayReportRepository();
        var service = CreateService(
            reports,
            project: CreateProject(ids),
            roleName: SalesRole,
            productionRequest: CreateProductionRequest(ids),
            productionDeadline: new DateOnly(2026, 9, 15));

        var result = await service.CreateProductionReportAsync(
            ids.ProjectId,
            ids.SalesId,
            new CreateProductionDelayReportRequestDto
            {
                ProductionRequestId = ids.ProductionRequestId,
                ProductionReasonCode = " MATERIAL_DELAY ",
                ReasonDetail = " Supplier delay "
            });

        Assert.Equal(201, result.Status);
        Assert.Equal("Production delay report recorded successfully.", result.Message);
        Assert.Equal(ids.ProjectId, result.Data!.ProjectId);
        Assert.Equal("PRODUCTION", result.Data.ReportPhase);
        Assert.Equal("MATERIAL_DELAY", result.Data.ProductionReasonCode);
        Assert.Null(result.Data.DeliveryReasonCode);
        Assert.Equal("Supplier delay", result.Data.ReasonDetail);
        Assert.Single(reports.AddedReports);
        Assert.Equal(1, reports.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateProductionReportAsync_WithAssignedProductionStaff_CreatesReport()
    {
        var ids = CreateIds();
        var reports = new FakeOperationalDelayReportRepository();
        var productionRequests = new FakeDelayProductionRequestRepository
        {
            ProductionRequest = CreateProductionRequest(ids),
            HasViewableAssignedRequest = true
        };
        var service = CreateService(
            reports,
            project: CreateProject(ids),
            roleName: ProductionRole,
            productionRequests: productionRequests,
            productionDeadline: new DateOnly(2026, 9, 20));

        var result = await service.CreateProductionReportAsync(
            ids.ProjectId,
            ids.ProductionId,
            new CreateProductionDelayReportRequestDto
            {
                ProductionRequestId = ids.ProductionRequestId,
                ProductionReasonCode = "OTHER",
                ReasonDetail = "Machine maintenance"
            });

        Assert.Equal(201, result.Status);
        Assert.Equal(ids.ProductionId, result.Data!.ReportedBy);
    }

    [Fact]
    public async Task CreateProductionReportAsync_WithUnassignedProduction_ReturnsForbidden()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: CreateProject(ids),
            roleName: ProductionRole,
            productionRequest: CreateProductionRequest(ids with { ProductionId = Guid.NewGuid() }),
            productionDeadline: new DateOnly(2026, 9, 20));

        var result = await service.CreateProductionReportAsync(
            ids.ProjectId,
            ids.ProductionId,
            ValidProductionRequest(ids));

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateProductionReportAsync_WithCustomerRole_ReturnsForbidden()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: CreateProject(ids),
            roleName: CustomerRole,
            productionRequest: CreateProductionRequest(ids),
            productionDeadline: new DateOnly(2026, 9, 20));

        var result = await service.CreateProductionReportAsync(
            ids.ProjectId,
            ids.CustomerId,
            ValidProductionRequest(ids));

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateProductionReportAsync_WhenProductionDeadlineMissing_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: CreateProject(ids),
            roleName: SalesRole,
            productionRequest: CreateProductionRequest(ids),
            productionDeadline: null);

        var result = await service.CreateProductionReportAsync(
            ids.ProjectId,
            ids.SalesId,
            ValidProductionRequest(ids));

        Assert.Equal(400, result.Status);
        Assert.Equal(OperationalDelayReportErrorCodes.ProductionDeadlineMissing, result.ErrorCode);
    }

    [Fact]
    public async Task CreateProductionReportAsync_WhenProductionRequestProjectMismatch_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var productionRequest = CreateProductionRequest(ids);
        productionRequest.ProjectId = Guid.NewGuid();
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: CreateProject(ids),
            roleName: SalesRole,
            productionRequest: productionRequest,
            productionDeadline: new DateOnly(2026, 9, 20));

        var result = await service.CreateProductionReportAsync(
            ids.ProjectId,
            ids.SalesId,
            ValidProductionRequest(ids));

        Assert.Equal(400, result.Status);
        Assert.Equal(OperationalDelayReportErrorCodes.ProductionRequestProjectMismatch, result.ErrorCode);
    }

    [Theory]
    [InlineData("", "Reason detail is required.")]
    [InlineData("   ", "Reason detail is required.")]
    public async Task CreateProductionReportAsync_WhenReasonDetailMissing_ReturnsBadRequest(
        string reasonDetail,
        string expectedMessage)
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: CreateProject(ids),
            roleName: SalesRole);

        var result = await service.CreateProductionReportAsync(
            ids.ProjectId,
            ids.SalesId,
            new CreateProductionDelayReportRequestDto
            {
                ProductionRequestId = ids.ProductionRequestId,
                ProductionReasonCode = "MATERIAL_DELAY",
                ReasonDetail = reasonDetail
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedMessage, result.Message);
    }

    [Fact]
    public async Task CreateDeliveryReportAsync_WithTargetCompletionDate_CreatesReport()
    {
        var ids = CreateIds();
        var reports = new FakeOperationalDelayReportRepository();
        var project = CreateProject(ids);
        project.TargetCompletionDate = new DateOnly(2026, 10, 1);
        var service = CreateService(
            reports,
            project: project,
            roleName: SalesRole,
            order: CreateOrder(ids));

        var result = await service.CreateDeliveryReportAsync(
            ids.ProjectId,
            ids.SalesId,
            new CreateDeliveryDelayReportRequestDto
            {
                OrderId = ids.OrderId,
                DeliveryReasonCode = "SITE_NOT_READY",
                ReasonDetail = "Traffic delay"
            });

        Assert.Equal(201, result.Status);
        Assert.Equal("DELIVERY", result.Data!.ReportPhase);
        Assert.Null(result.Data.ProductionReasonCode);
        Assert.Equal("SITE_NOT_READY", result.Data.DeliveryReasonCode);
        Assert.Equal(ids.OrderId, result.Data.OrderId);
        Assert.Equal(new DateOnly(2026, 10, 1), result.Data.DeadlineSnapshot);
        Assert.Single(reports.AddedReports);
    }

    [Fact]
    public async Task CreateDeliveryReportAsync_WhenTargetCompletionDateMissing_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: CreateProject(ids),
            roleName: SalesRole);

        var result = await service.CreateDeliveryReportAsync(
            ids.ProjectId,
            ids.SalesId,
            new CreateDeliveryDelayReportRequestDto
            {
                DeliveryReasonCode = "OTHER",
                ReasonDetail = "Late shipment"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OperationalDelayReportErrorCodes.TargetCompletionDateMissing, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryReportAsync_WhenOrderProjectMismatch_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var project = CreateProject(ids);
        project.TargetCompletionDate = new DateOnly(2026, 10, 1);
        var order = CreateOrder(ids);
        order.ProjectId = Guid.NewGuid();
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: project,
            roleName: SalesRole,
            order: order);

        var result = await service.CreateDeliveryReportAsync(
            ids.ProjectId,
            ids.SalesId,
            new CreateDeliveryDelayReportRequestDto
            {
                OrderId = ids.OrderId,
                DeliveryReasonCode = "OTHER",
                ReasonDetail = "Wrong order"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OperationalDelayReportErrorCodes.OrderProjectMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryReportAsync_WhenDeliveryOrderMismatch_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var project = CreateProject(ids);
        project.TargetCompletionDate = new DateOnly(2026, 10, 1);
        var delivery = new Delivery
        {
            DeliveryId = ids.DeliveryId,
            OrderId = Guid.NewGuid(),
            Status = DeliveryStatus.IN_PROGRESS,
            CreatedAt = DateTime.UtcNow
        };
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: project,
            roleName: SalesRole,
            delivery: delivery);

        var result = await service.CreateDeliveryReportAsync(
            ids.ProjectId,
            ids.SalesId,
            new CreateDeliveryDelayReportRequestDto
            {
                DeliveryId = ids.DeliveryId,
                DeliveryReasonCode = "OTHER",
                ReasonDetail = "Delivery mismatch"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OperationalDelayReportErrorCodes.DeliveryProjectMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task GetByProjectAsync_WithAssignedSales_ReturnsReports()
    {
        var ids = CreateIds();
        var listItem = CreateListItem(ids);
        var service = CreateService(
            new FakeOperationalDelayReportRepository { ListItems = [listItem] },
            project: CreateProject(ids),
            roleName: SalesRole);

        var result = await service.GetByProjectAsync(
            ids.ProjectId,
            ids.SalesId,
            OperationalDelayPhase.PRODUCTION);

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(listItem.OperationalDelayReportId, result.Data.Items[0].OperationalDelayReportId);
    }

    [Fact]
    public async Task GetByProjectAsync_WithProductionWithoutAssignment_ReturnsForbidden()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: CreateProject(ids),
            roleName: ProductionRole,
            productionRequests: new FakeDelayProductionRequestRepository { HasViewableAssignedRequest = false });

        var result = await service.GetByProjectAsync(
            ids.ProjectId,
            ids.ProductionId,
            OperationalDelayPhase.PRODUCTION);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_WithAdmin_ReturnsReport()
    {
        var ids = CreateIds();
        var detail = CreateDetail(ids);
        var service = CreateService(
            new FakeOperationalDelayReportRepository { Detail = detail },
            project: CreateProject(ids),
            roleName: AdminRole);

        var result = await service.GetDetailAsync(detail.OperationalDelayReportId, Guid.NewGuid());

        Assert.Equal(200, result.Status);
        Assert.Equal(detail.ProjectName, result.Data!.ProjectName);
    }

    [Fact]
    public async Task GetDetailAsync_WhenReportMissing_ReturnsNotFound()
    {
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: null,
            roleName: AdminRole);

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(OperationalDelayReportErrorCodes.ReportNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateProductionReportAsync_WithInvalidProductionReasonCode_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: CreateProject(ids),
            roleName: SalesRole,
            productionRequest: CreateProductionRequest(ids),
            productionDeadline: new DateOnly(2026, 9, 20));

        var result = await service.CreateProductionReportAsync(
            ids.ProjectId,
            ids.SalesId,
            new CreateProductionDelayReportRequestDto
            {
                ProductionRequestId = ids.ProductionRequestId,
                ProductionReasonCode = "WEATHER",
                ReasonDetail = "Wrong phase reason"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal("Production reason code is invalid.", result.Message);
    }

    [Fact]
    public async Task CreateProductionReportAsync_WithDeliveryReasonField_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: CreateProject(ids),
            roleName: SalesRole);

        var result = await service.CreateProductionReportAsync(
            ids.ProjectId,
            ids.SalesId,
            new CreateProductionDelayReportRequestDto
            {
                ProductionRequestId = ids.ProductionRequestId,
                ProductionReasonCode = "MATERIAL_DELAY",
                DeliveryReasonCode = "WEATHER",
                ReasonDetail = "Both fields"
            });

        Assert.Equal(400, result.Status);
        Assert.Contains("Delivery reason code is not accepted", result.Message);
    }

    [Fact]
    public async Task CreateDeliveryReportAsync_WithInvalidDeliveryReasonCode_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var project = CreateProject(ids);
        project.TargetCompletionDate = new DateOnly(2026, 10, 1);
        var service = CreateService(
            new FakeOperationalDelayReportRepository(),
            project: project,
            roleName: SalesRole);

        var result = await service.CreateDeliveryReportAsync(
            ids.ProjectId,
            ids.SalesId,
            new CreateDeliveryDelayReportRequestDto
            {
                DeliveryReasonCode = "MATERIAL_DELAY",
                ReasonDetail = "Wrong phase reason"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal("Delivery reason code is invalid.", result.Message);
    }

    [Fact]
    public async Task GetDetailAsync_WithMigratedProductionReason_ReturnsPhaseSpecificFields()
    {
        var ids = CreateIds();
        var detail = CreateDetail(ids);
        var service = CreateService(
            new FakeOperationalDelayReportRepository { Detail = detail },
            project: CreateProject(ids),
            roleName: AdminRole);

        var result = await service.GetDetailAsync(detail.OperationalDelayReportId, Guid.NewGuid());

        Assert.Equal(200, result.Status);
        Assert.Equal("MATERIAL_DELAY", result.Data!.ProductionReasonCode);
        Assert.Null(result.Data.DeliveryReasonCode);
    }

    private static OperationalDelayReportService CreateService(
        FakeOperationalDelayReportRepository reports,
        Project? project,
        string roleName,
        ProductionRequest? productionRequest = null,
        DateOnly? productionDeadline = null,
        Order? order = null,
        Delivery? delivery = null,
        FakeDelayProductionRequestRepository? productionRequests = null)
    {
        return new OperationalDelayReportService(
            reports,
            new FakeDelayProjectRepository(project, roleName),
            productionRequests ?? new FakeDelayProductionRequestRepository
            {
                ProductionRequest = productionRequest,
                HasViewableAssignedRequest = productionRequest?.AssignedTo is not null
            },
            new FakeDelayOrderRepository(order),
            new FakeDelayDeliveryRepository(delivery),
            new FakeDelayPhaseDeadlineService(productionDeadline),
            TestUnitOfWork.ForSaveChanges(_ =>
            {
                reports.SaveChangesCallCount++;
                return Task.FromResult(1);
            }));
    }

    private static CreateProductionDelayReportRequestDto ValidProductionRequest(TestIds ids)
    {
        return new CreateProductionDelayReportRequestDto
        {
            ProductionRequestId = ids.ProductionRequestId,
            ProductionReasonCode = "OTHER",
            ReasonDetail = "Delay reason"
        };
    }

    private static TestIds CreateIds()
    {
        return new TestIds(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
    }

    private static Project CreateProject(TestIds ids)
    {
        return new Project
        {
            ProjectId = ids.ProjectId,
            CustomerId = ids.CustomerId,
            AssignedSalesId = ids.SalesId,
            ProjectName = "Delay Project",
            Status = ProjectStatus.IN_PRODUCTION
        };
    }

    private static ProductionRequest CreateProductionRequest(TestIds ids)
    {
        return new ProductionRequest
        {
            ProductionRequestId = ids.ProductionRequestId,
            ProjectId = ids.ProjectId,
            OrderId = ids.OrderId,
            AssignedTo = ids.ProductionId,
            ProductionCode = "PRD-001",
            Status = ProductionRequestStatus.IN_PRODUCTION,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Order CreateOrder(TestIds ids)
    {
        return new Order
        {
            OrderId = ids.OrderId,
            ProjectId = ids.ProjectId,
            CustomerId = ids.CustomerId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-001",
            Status = OrderStatus.IN_PRODUCTION,
            VatRate = 0.08m,
            VatAmount = 0m,
            FinalTotalAmount = 100m,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static OperationalDelayReportListItemReadModel CreateListItem(TestIds ids)
    {
        return new OperationalDelayReportListItemReadModel
        {
            OperationalDelayReportId = Guid.NewGuid(),
            ProjectId = ids.ProjectId,
            ReportPhase = OperationalDelayPhase.PRODUCTION,
            ProductionRequestId = ids.ProductionRequestId,
            DeadlineSnapshot = new DateOnly(2026, 9, 15),
            DelayState = OperationalDelayState.AT_RISK,
            ProductionReasonCode = ProductionDelayReasonCode.OTHER,
            ReasonDetail = "Supplier delay",
            ReportedBy = ids.SalesId,
            ReporterName = "Sales User",
            ReportedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static OperationalDelayReportDetailReadModel CreateDetail(TestIds ids)
    {
        return new OperationalDelayReportDetailReadModel
        {
            OperationalDelayReportId = Guid.NewGuid(),
            ProjectId = ids.ProjectId,
            ProjectName = "Delay Project",
            ReportPhase = OperationalDelayPhase.PRODUCTION,
            ProductionRequestId = ids.ProductionRequestId,
            DeadlineSnapshot = new DateOnly(2026, 9, 15),
            DelayState = OperationalDelayState.OVERDUE,
            ProductionReasonCode = ProductionDelayReasonCode.MATERIAL_DELAY,
            ReasonDetail = "Overdue production",
            ReportedBy = ids.SalesId,
            ReporterName = "Sales User",
            ReportedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed record TestIds(
        Guid ProjectId,
        Guid SalesId,
        Guid ProductionId,
        Guid CustomerId,
        Guid ProductionRequestId,
        Guid OrderId,
        Guid DeliveryId);

    private sealed class FakeOperationalDelayReportRepository : IOperationalDelayReportRepository
    {
        public List<OperationalDelayReport> AddedReports { get; } = [];
        public OperationalDelayReportDetailReadModel? Detail { get; init; }
        public IReadOnlyList<OperationalDelayReportListItemReadModel> ListItems { get; init; } = [];
        public int SaveChangesCallCount { get; set; }

        public Task<OperationalDelayReportDetailReadModel?> GetDetailAsync(
            Guid reportId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Detail?.OperationalDelayReportId == reportId ? Detail : null);

        public Task<IReadOnlyList<OperationalDelayReportListItemReadModel>> GetByProjectAsync(
            Guid projectId,
            OperationalDelayPhase phase,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ListItems);

        public Task AddAsync(OperationalDelayReport entity, CancellationToken cancellationToken = default)
        {
            AddedReports.Add(entity);
            return Task.CompletedTask;
        }

        public IQueryable<OperationalDelayReport> Query() => AddedReports.AsQueryable();
        public Task<OperationalDelayReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<OperationalDelayReport?>(null);
        public Task<IReadOnlyList<OperationalDelayReport>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OperationalDelayReport>>(AddedReports);
        public Task AddRangeAsync(IEnumerable<OperationalDelayReport> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(OperationalDelayReport entity) { }
        public void Remove(OperationalDelayReport entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeDelayProjectRepository : IProjectRepository
    {
        private readonly Project? _project;
        private readonly string _roleName;

        public FakeDelayProjectRepository(Project? project, string roleName)
        {
            _project = project;
            _roleName = roleName;
        }

        public Task<Project?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult(_project?.ProjectId == projectId ? _project : null);

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(_roleName);

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectDetailReadModel?>(null);

        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default)
            => Task.FromResult<DesignerAccountReadModel?>(null);

        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
            ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);

        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);

        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectSearchIndexItemReadModel?>(null);

        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);

        public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>([]);
        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Project entity) { }
        public void Remove(Project entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeDelayProductionRequestRepository : IProductionRequestRepository
    {
        public ProductionRequest? ProductionRequest { get; init; }
        public bool HasViewableAssignedRequest { get; init; }

        public Task<ProductionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(ProductionRequest?.ProductionRequestId == id ? ProductionRequest : null);

        public Task<bool> HasViewableAssignedRequestAsync(
            Guid projectId,
            Guid productionAccountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HasViewableAssignedRequest);

        public Task<DateOnly?> GetMaxOperationalProductionDateAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<DateOnly?>(null);
        public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasActiveRequestForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<int> CountCreatedOnAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<List<OrderItem>> GetProductOrderItemsAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(new List<OrderItem>());
        public Task AddItemsAsync(List<ProductionItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsActiveProductionStaffAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Infrastructure.ReadModels.Production.ProductionAssigneeReadModel?> GetAssigneeAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<Infrastructure.ReadModels.Production.ProductionAssigneeReadModel?>(null);
        public Task<List<Infrastructure.ReadModels.Production.AvailableProductionStaffReadModel>> GetAvailableStaffAsync(string? search, CancellationToken cancellationToken = default) => Task.FromResult(new List<Infrastructure.ReadModels.Production.AvailableProductionStaffReadModel>());
        public Task<List<Infrastructure.ReadModels.Production.ProductionRequestListItemReadModel>> GetQueueAsync(Infrastructure.ReadModels.Production.ProductionRequestQueueReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(new List<Infrastructure.ReadModels.Production.ProductionRequestListItemReadModel>());
        public Task<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?> GetDetailAsync(Guid productionRequestId, CancellationToken cancellationToken = default) => Task.FromResult<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?>(null);
        public Task<ProductionItem?> GetItemByIdAsync(Guid productionItemId, CancellationToken cancellationToken = default) => Task.FromResult<ProductionItem?>(null);
        public Task<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?> GetDetailByItemIdAsync(Guid productionItemId, CancellationToken cancellationToken = default) => Task.FromResult<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?>(null);
        public void UpdateItem(ProductionItem item) { }
        public IQueryable<ProductionRequest> Query() => Enumerable.Empty<ProductionRequest>().AsQueryable();
        public Task<IReadOnlyList<ProductionRequest>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductionRequest>>([]);
        public Task AddAsync(ProductionRequest entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<ProductionRequest> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(ProductionRequest entity) { }
        public void Remove(ProductionRequest entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeDelayOrderRepository : IOrderRepository
    {
        private readonly Order? _order;

        public FakeDelayOrderRepository(Order? order) => _order = order;

        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_order?.OrderId == orderId ? _order : null);

        public Task<IReadOnlyList<OrderListItemReadModel>> GetByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrderListItemReadModel>>([]);

        public Task<OrderDetailReadModel?> GetDetailAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<OrderDetailReadModel?>(null);

        public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void UpdateItem(OrderItem item) { }

        public IQueryable<Order> Query() => Enumerable.Empty<Order>().AsQueryable();
        public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>([]);
        public Task AddAsync(Order entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Order> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Order entity) { }
        public void Remove(Order entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeDelayDeliveryRepository : IDeliveryRepository
    {
        private readonly Delivery? _delivery;

        public FakeDelayDeliveryRepository(Delivery? delivery) => _delivery = delivery;

        public Task<Delivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_delivery?.DeliveryId == deliveryId ? _delivery : null);

        public Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddItemAsync(DeliveryItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DeliveryDetailReadModel?> GetDetailAsync(Guid orderId, Guid deliveryId, CancellationToken cancellationToken = default) => Task.FromResult<DeliveryDetailReadModel?>(null);
        public Task<IReadOnlyList<DeliveryListItemReadModel>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeliveryListItemReadModel>>([]);
        public Task<IReadOnlyList<DeliveryItem>> GetItemsByDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeliveryItem>>([]);
        public Task<DeliveryItem?> GetItemByIdAsync(Guid deliveryItemId, CancellationToken cancellationToken = default) => Task.FromResult<DeliveryItem?>(null);
        public void Update(Delivery delivery) { }
    }

    private sealed class FakeDelayPhaseDeadlineService : IProjectPhaseDeadlineService
    {
        private readonly DateOnly? _productionDeadline;

        public FakeDelayPhaseDeadlineService(DateOnly? productionDeadline) => _productionDeadline = productionDeadline;

        public Task<DateOnly?> GetProductionDeadlineAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult(_productionDeadline);

        public Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> UpsertAsync(
            Guid projectId,
            Guid currentUserId,
            UpsertProjectPhaseDeadlinesRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(new ProjectPhaseDeadlinePlanDto()));

        public Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> GetAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(new ProjectPhaseDeadlinePlanDto()));

        public Task MarkStartedOnceAsync(
            Guid projectId,
            ProjectPhaseType phase,
            DateTime startedAt,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkCompletedOnceAsync(
            Guid projectId,
            ProjectPhaseType phase,
            DateTime completedAt,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ServiceResult<ProjectProductionPhaseDeadlineResponseDto>> UpsertProductionDeadlineAsync(
            Guid projectId,
            Guid currentUserId,
            UpsertProductionPhaseDeadlineRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Success(
                new ProjectProductionPhaseDeadlineResponseDto()));

        public Task<ServiceResult<DateOnly>> StageProposalDeadlineForDesignerAssignmentAsync(
            Guid projectId,
            Guid currentUserId,
            DateOnly proposalDeadline,
            DateOnly? targetCompletionDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<DateOnly>.Success(proposalDeadline));
        public Task<bool> HasProductionDeadlineAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(_productionDeadline.HasValue);
    }
}
