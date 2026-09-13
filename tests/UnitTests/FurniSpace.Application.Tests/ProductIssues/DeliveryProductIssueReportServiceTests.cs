#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.DTOs.ProductIssues;
using FurniSpace.Application.Services.ProductIssues;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.ReadModels.ProductIssues;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.ProductIssues;

public sealed class DeliveryProductIssueReportServiceTests
{
    [Fact]
    public async Task CreateAsync_WithDeliveredItemAndEvidence_CreatesReport()
    {
        var ids = CreateIds();
        var issues = new FakeProductIssueRepository();
        var files = new FakeProductIssueFileRepository();
        var storage = new FakeProductIssueStorageService();
        var service = CreateService(
            issues,
            files,
            storage,
            roleName: "CUSTOMER",
            project: CreateProject(ids),
            order: CreateOrder(ids),
            orderItem: CreateOrderItem(ids, deliveredQuantity: 2));

        var result = await service.CreateAsync(
            ids.OrderId,
            ids.CustomerId,
            new CreateProductIssueRequestDto
            {
                OrderItemId = ids.OrderItemId,
                IssueType = DeliveryProductIssueType.DAMAGED,
                Description = " Corner chipped ",
                AffectedQuantity = 1,
                EvidenceFiles =
                [
                    new ProductIssueEvidenceUploadDto
                    {
                        Content = new MemoryStream(Encoding.UTF8.GetBytes("photo")),
                        OriginalFileName = "damage.jpg",
                        ContentType = "image/jpeg",
                        FileSizeBytes = 5
                    }
                ]
            });

        Assert.Equal(201, result.Status);
        Assert.Equal("Product issue report submitted successfully.", result.Message);
        Assert.Equal("Corner chipped", result.Data!.Description);
        Assert.Equal("DAMAGED", result.Data.IssueType);
        Assert.Single(issues.AddedIssues);
        Assert.Single(files.StoredFiles);
        Assert.Single(files.FileLinks);
        Assert.Equal(FileType.PRODUCT_ISSUE_EVIDENCE, files.FileLinks[0].FileType);
        Assert.Equal(1, issues.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithSalesRole_ReturnsForbidden()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeProductIssueRepository(),
            new FakeProductIssueFileRepository(),
            new FakeProductIssueStorageService(),
            roleName: "SALES");

        var result = await service.CreateAsync(
            ids.OrderId,
            ids.SalesId,
            ValidCreateRequest(ids));

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateAsync_WhenNotDelivered_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeProductIssueRepository(),
            new FakeProductIssueFileRepository(),
            new FakeProductIssueStorageService(),
            roleName: "CUSTOMER",
            project: CreateProject(ids),
            order: CreateOrder(ids),
            orderItem: CreateOrderItem(ids, deliveredQuantity: 0));

        var result = await service.CreateAsync(ids.OrderId, ids.CustomerId, ValidCreateRequest(ids));

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductIssueErrorCodes.NotDelivered, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenAffectedQuantityExceedsDelivered_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeProductIssueRepository(),
            new FakeProductIssueFileRepository(),
            new FakeProductIssueStorageService(),
            roleName: "CUSTOMER",
            project: CreateProject(ids),
            order: CreateOrder(ids),
            orderItem: CreateOrderItem(ids, deliveredQuantity: 1));

        var request = ValidCreateRequest(ids);
        request.AffectedQuantity = 2;
        var result = await service.CreateAsync(ids.OrderId, ids.CustomerId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductIssueErrorCodes.InvalidAffectedQuantity, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenDeliveryItemMismatch_ReturnsBadRequest()
    {
        var ids = CreateIds();
        var deliveryItem = new DeliveryItem
        {
            DeliveryItemId = ids.DeliveryItemId,
            DeliveryId = ids.DeliveryId,
            OrderItemId = Guid.NewGuid(),
            Quantity = 1
        };
        var service = CreateService(
            new FakeProductIssueRepository(),
            new FakeProductIssueFileRepository(),
            new FakeProductIssueStorageService(),
            roleName: "CUSTOMER",
            project: CreateProject(ids),
            order: CreateOrder(ids),
            orderItem: CreateOrderItem(ids, deliveredQuantity: 1),
            deliveryItem: deliveryItem);

        var request = ValidCreateRequest(ids);
        request.DeliveryItemId = ids.DeliveryItemId;
        var result = await service.CreateAsync(ids.OrderId, ids.CustomerId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductIssueErrorCodes.DeliveryItemOrderItemMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenEvidenceFileTooLarge_ReturnsPayloadTooLarge()
    {
        var ids = CreateIds();
        var validator = new FakeProductIssueFileUploadValidator(
            FileUploadValidationResult.Failure(
                FileUploadValidationFailureKind.FileTooLarge,
                "File is too large."));
        var service = CreateService(
            new FakeProductIssueRepository(),
            new FakeProductIssueFileRepository(),
            new FakeProductIssueStorageService(),
            roleName: "CUSTOMER",
            project: CreateProject(ids),
            order: CreateOrder(ids),
            orderItem: CreateOrderItem(ids, deliveredQuantity: 1),
            fileUploadValidator: validator);

        var request = ValidCreateRequest(ids);
        request.EvidenceFiles =
        [
            new ProductIssueEvidenceUploadDto
            {
                Content = Stream.Null,
                OriginalFileName = "large.jpg",
                ContentType = "image/jpeg",
                FileSizeBytes = 999
            }
        ];
        var result = await service.CreateAsync(ids.OrderId, ids.CustomerId, request);

        Assert.Equal(413, result.Status);
    }

    [Fact]
    public async Task GetByOrderAsync_WithCustomerOwner_ReturnsIssues()
    {
        var ids = CreateIds();
        var listItem = CreateListItem(ids);
        var service = CreateService(
            new FakeProductIssueRepository { ListItems = [listItem] },
            new FakeProductIssueFileRepository(),
            new FakeProductIssueStorageService(),
            roleName: "CUSTOMER",
            project: CreateProject(ids),
            order: CreateOrder(ids));

        var result = await service.GetByOrderAsync(ids.OrderId, ids.CustomerId);

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task GetByOrderAsync_WithAssignedSales_ReturnsIssues()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeProductIssueRepository { ListItems = [CreateListItem(ids)] },
            new FakeProductIssueFileRepository(),
            new FakeProductIssueStorageService(),
            roleName: "SALES",
            project: CreateProject(ids),
            order: CreateOrder(ids));

        var result = await service.GetByOrderAsync(ids.OrderId, ids.SalesId);

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task GetByProjectAsync_WithDesigner_ReturnsForbidden()
    {
        var ids = CreateIds();
        var service = CreateService(
            new FakeProductIssueRepository(),
            new FakeProductIssueFileRepository(),
            new FakeProductIssueStorageService(),
            roleName: "DESIGNER",
            project: CreateProject(ids));

        var result = await service.GetByProjectAsync(ids.ProjectId, ids.DesignerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_WithAdmin_ReturnsIssue()
    {
        var ids = CreateIds();
        var detail = CreateDetail(ids);
        var service = CreateService(
            new FakeProductIssueRepository { Detail = detail },
            new FakeProductIssueFileRepository(),
            new FakeProductIssueStorageService(),
            roleName: "ADMIN",
            project: CreateProject(ids));

        var result = await service.GetDetailAsync(detail.DeliveryProductIssueReportId, Guid.NewGuid());

        Assert.Equal(200, result.Status);
        Assert.Equal(detail.ProjectName, result.Data!.ProjectName);
        Assert.Single(result.Data.EvidenceFiles);
    }

    private static DeliveryProductIssueReportService CreateService(
        FakeProductIssueRepository issues,
        FakeProductIssueFileRepository files,
        FakeProductIssueStorageService storage,
        string roleName,
        Project? project = null,
        Order? order = null,
        OrderItem? orderItem = null,
        DeliveryItem? deliveryItem = null,
        Delivery? delivery = null,
        IFileUploadValidator? fileUploadValidator = null,
        FakeProductIssueProductionRequestRepository? productionRequests = null)
    {
        return new DeliveryProductIssueReportService(
            issues,
            new FakeProductIssueOrderRepository(order, orderItem),
            new FakeProductIssueProjectRepository(project, roleName),
            productionRequests ?? new FakeProductIssueProductionRequestRepository(),
            new FakeProductIssueDeliveryRepository(delivery, deliveryItem),
            files,
            TestUnitOfWork.ForTransaction(
                _ => Task.CompletedTask,
                _ =>
                {
                    issues.SaveChangesCallCount++;
                    return Task.FromResult(1);
                },
                _ => Task.CompletedTask,
                _ => Task.CompletedTask),
            storage,
            fileUploadValidator ?? new FakeProductIssueFileUploadValidator(FileUploadValidationResult.Success()),
            Options.Create(new FirebaseStorageSettings
            {
                Bucket = "test-bucket",
                ProjectFilesPrefix = "projects"
            }));
    }

    private static CreateProductIssueRequestDto ValidCreateRequest(TestIds ids)
    {
        return new CreateProductIssueRequestDto
        {
            OrderItemId = ids.OrderItemId,
            IssueType = DeliveryProductIssueType.QUALITY_DEFECT,
            Description = "Surface scratch"
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
            AssignedDesignerId = ids.DesignerId,
            ProjectName = "Issue Project",
            Status = ProjectStatus.DELIVERING
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
            OrderCode = "ORD-ISSUE",
            Status = OrderStatus.DELIVERING,
            VatRate = 0.08m,
            VatAmount = 0m,
            FinalTotalAmount = 100m,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static OrderItem CreateOrderItem(TestIds ids, int deliveredQuantity)
    {
        return new OrderItem
        {
            OrderItemId = ids.OrderItemId,
            OrderId = ids.OrderId,
            ProductNameSnapshot = "Oak Chair",
            Quantity = 2,
            DeliveredQuantity = deliveredQuantity,
            Status = OrderItemStatus.DELIVERED,
            UnitPrice = 50m,
            DiscountAmount = 0m,
            SubtotalAmount = 100m
        };
    }

    private static DeliveryProductIssueReportListItemReadModel CreateListItem(TestIds ids)
    {
        return new DeliveryProductIssueReportListItemReadModel
        {
            DeliveryProductIssueReportId = Guid.NewGuid(),
            ProjectId = ids.ProjectId,
            OrderId = ids.OrderId,
            OrderItemId = ids.OrderItemId,
            IssueType = DeliveryProductIssueType.DAMAGED,
            Description = "Damaged corner",
            ReportedBy = ids.CustomerId,
            ReporterName = "Customer User",
            ReportedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static DeliveryProductIssueReportDetailReadModel CreateDetail(TestIds ids)
    {
        return new DeliveryProductIssueReportDetailReadModel
        {
            DeliveryProductIssueReportId = Guid.NewGuid(),
            ProjectId = ids.ProjectId,
            ProjectName = "Issue Project",
            OrderId = ids.OrderId,
            OrderItemId = ids.OrderItemId,
            ProductNameSnapshot = "Oak Chair",
            IssueType = DeliveryProductIssueType.DAMAGED,
            Description = "Damaged corner",
            ReportedBy = ids.CustomerId,
            ReporterName = "Customer User",
            ReportedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            EvidenceFiles =
            [
                new DeliveryProductIssueEvidenceReadModel
                {
                    FileId = Guid.NewGuid(),
                    FileLinkId = Guid.NewGuid(),
                    OriginalFileName = "damage.jpg",
                    FileUrl = "https://files.example/damage.jpg",
                    MimeType = "image/jpeg",
                    FileSizeBytes = 100
                }
            ]
        };
    }

    private sealed record TestIds(
        Guid ProjectId,
        Guid CustomerId,
        Guid SalesId,
        Guid DesignerId,
        Guid OrderId,
        Guid OrderItemId,
        Guid DeliveryId,
        Guid DeliveryItemId);

    private sealed class FakeProductIssueRepository : IDeliveryProductIssueReportRepository
    {
        public List<DeliveryProductIssueReport> AddedIssues { get; } = [];
        public DeliveryProductIssueReportDetailReadModel? Detail { get; init; }
        public IReadOnlyList<DeliveryProductIssueReportListItemReadModel> ListItems { get; init; } = [];
        public int SaveChangesCallCount { get; set; }

        public Task<DeliveryProductIssueReportDetailReadModel?> GetDetailAsync(
            Guid issueId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Detail?.DeliveryProductIssueReportId == issueId ? Detail : null);

        public Task<IReadOnlyList<DeliveryProductIssueReportListItemReadModel>> GetByOrderAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ListItems);

        public Task<IReadOnlyList<DeliveryProductIssueReportListItemReadModel>> GetByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ListItems);

        public Task AddAsync(DeliveryProductIssueReport entity, CancellationToken cancellationToken = default)
        {
            AddedIssues.Add(entity);
            return Task.CompletedTask;
        }

        public IQueryable<DeliveryProductIssueReport> Query() => AddedIssues.AsQueryable();
        public Task<DeliveryProductIssueReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<DeliveryProductIssueReport?>(null);
        public Task<IReadOnlyList<DeliveryProductIssueReport>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeliveryProductIssueReport>>(AddedIssues);
        public Task AddRangeAsync(IEnumerable<DeliveryProductIssueReport> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(DeliveryProductIssueReport entity) { }
        public void Remove(DeliveryProductIssueReport entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeProductIssueProjectRepository : IProjectRepository
    {
        private readonly Project? _project;
        private readonly string _roleName;

        public FakeProductIssueProjectRepository(Project? project, string roleName)
        {
            _project = project;
            _roleName = roleName;
        }

        public Task<Project?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult(_project?.ProjectId == projectId ? _project : null);

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(_roleName);

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectDetailReadModel?>(null);
        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) => Task.FromResult<DesignerAccountReadModel?>(null);
        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);
        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);
        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectSearchIndexItemReadModel?>(null);
        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);
        public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>([]);
        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Project entity) { }
        public void Remove(Project entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeProductIssueOrderRepository : IOrderRepository
    {
        private readonly Order? _order;
        private readonly OrderItem? _orderItem;

        public FakeProductIssueOrderRepository(Order? order, OrderItem? orderItem)
        {
            _order = order;
            _orderItem = orderItem;
        }

        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_order?.OrderId == orderId ? _order : null);

        public Task<OrderItem?> GetItemByIdAsync(Guid orderItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(_orderItem?.OrderItemId == orderItemId ? _orderItem : null);

        public Task<IReadOnlyList<OrderListItemReadModel>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OrderListItemReadModel>>([]);
        public Task<OrderDetailReadModel?> GetDetailAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult<OrderDetailReadModel?>(null);
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

    private sealed class FakeProductIssueDeliveryRepository : IDeliveryRepository
    {
        private readonly Delivery? _delivery;
        private readonly DeliveryItem? _deliveryItem;

        public FakeProductIssueDeliveryRepository(Delivery? delivery, DeliveryItem? deliveryItem)
        {
            _delivery = delivery;
            _deliveryItem = deliveryItem;
        }

        public Task<DeliveryItem?> GetItemByIdAsync(Guid deliveryItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(_deliveryItem?.DeliveryItemId == deliveryItemId ? _deliveryItem : null);

        public Task<Delivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_delivery?.DeliveryId == deliveryId ? _delivery : null);

        public Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddItemAsync(DeliveryItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DeliveryDetailReadModel?> GetDetailAsync(Guid orderId, Guid deliveryId, CancellationToken cancellationToken = default) => Task.FromResult<DeliveryDetailReadModel?>(null);
        public Task<IReadOnlyList<DeliveryListItemReadModel>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeliveryListItemReadModel>>([]);
        public Task<IReadOnlyList<DeliveryItem>> GetItemsByDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeliveryItem>>([]);
        public void Update(Delivery delivery) { }
    }

    private sealed class FakeProductIssueProductionRequestRepository : IProductionRequestRepository
    {
        public bool HasViewableAssignedRequest { get; init; }

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
        public Task<ProductionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ProductionRequest?>(null);
        public Task<IReadOnlyList<ProductionRequest>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductionRequest>>([]);
        public Task AddAsync(ProductionRequest entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<ProductionRequest> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(ProductionRequest entity) { }
        public void Remove(ProductionRequest entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeProductIssueFileRepository : IProjectFileRepository
    {
        public List<StoredFile> StoredFiles { get; } = [];
        public List<FileLink> FileLinks { get; } = [];

        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default)
        {
            StoredFiles.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default)
        {
            FileLinks.Add(fileLink);
            return Task.CompletedTask;
        }

        public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(string referenceType, Guid referenceId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<FileMetadataReadModel?> GetFileMetadataAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<FileMetadataReadModel?>(null);
        public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(FileReferenceQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(new FileReferencePageReadModel());
        public Task<FileLinkReadModel?> GetFileLinkAsync(Guid fileLinkId, CancellationToken cancellationToken = default) => Task.FromResult<FileLinkReadModel?>(null);
        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks) { }
        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(string referenceType, IReadOnlyList<Guid> referenceIds, bool customerVisibleOnly, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CatalogFileReadModel>>([]);
        public Task<int> CountProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>([]);
        public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(Guid productId, Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<ProductPreviewImageReadModel?>(null);
        public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public Task<int> CountProductVersionPreviewFilesAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileSearchIndexItemReadModel?>(null);
        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(Guid projectId, string query, int page, int limit, bool customerVisibleOnly, Guid? customerAccountId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
        public Task<int> CountSearchByProjectAsync(Guid projectId, string query, bool customerVisibleOnly, Guid? customerAccountId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> HasProjectFileWithTypesAsync(Guid projectId, IReadOnlyCollection<FileType> fileTypes, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public IQueryable<StoredFile> Query() => Enumerable.Empty<StoredFile>().AsQueryable();
        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<StoredFile?>(null);
        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StoredFile>>([]);
        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(StoredFile entity) { }
        public void Remove(StoredFile entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeProductIssueStorageService : IFileStorageService
    {
        public Task<StorageUploadResult> UploadAsync(StorageUploadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StorageUploadResult
            {
                ObjectName = request.ObjectName,
                PublicUrl = $"https://files.example/{request.ObjectName}"
            });
        }

        public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProductIssueFileUploadValidator : IFileUploadValidator
    {
        private readonly FileUploadValidationResult _result;

        public FakeProductIssueFileUploadValidator(FileUploadValidationResult result) => _result = result;

        public FileUploadValidationResult Validate(IFileUploadPayload payload) => _result;
    }
}
