#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.ProductIssues;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurniSpace.Application.Tests.ProductIssues;

public sealed class ProductIssueNotificationSupportTests
{
    [Fact]
    public async Task TryDispatchReportedAsync_NotifiesSalesProductionAndAdmins()
    {
        var ids = CreateIds();
        var dispatcher = new CapturingDispatcher();
        var issue = CreateIssue(ids);
        var project = CreateProject(ids);
        var order = CreateOrder(ids);
        var orderItem = CreateOrderItem(ids);
        var productionRequests = new FakeProductionRepository
        {
            OrderAssignees = [ids.ProductionId]
        };

        await ProductIssueNotificationSupport.TryDispatchReportedAsync(
            dispatcher,
            new FakeProjectRepository(ids),
            productionRequests,
            NullLogger.Instance,
            issue,
            project,
            order,
            orderItem);

        var dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(NotificationType.ProductIssueReported, dispatch.Type);
        Assert.Contains(ids.SalesId, dispatch.Receivers);
        Assert.Contains(ids.ProductionId, dispatch.Receivers);
        Assert.Contains(ids.AdminId, dispatch.Receivers);
        Assert.Equal("DELIVERY_PRODUCT_ISSUE_REPORT", dispatch.ReferenceType);
        Assert.Equal(issue.DeliveryProductIssueReportId, dispatch.ReferenceId);
        Assert.Equal("product_issue.reported", dispatch.SignalREventName);
        Assert.Equal("Oak Chair", dispatch.Parameters["ProductName"]);
        Assert.Equal("ORD-001", dispatch.Parameters["OrderCode"]);
        Assert.Equal(2, dispatch.Metadata!["affectedQuantity"]);
    }

    [Fact]
    public async Task TryDispatchReportedAsync_WhenDispatcherMissing_DoesNotThrow()
    {
        var ids = CreateIds();

        var exception = await Record.ExceptionAsync(() =>
            ProductIssueNotificationSupport.TryDispatchReportedAsync(
                notifications: null,
                new FakeProjectRepository(ids),
                new FakeProductionRepository(),
                NullLogger.Instance,
                CreateIssue(ids),
                CreateProject(ids),
                CreateOrder(ids),
                CreateOrderItem(ids)));

        Assert.Null(exception);
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
            AssignedSalesId = ids.SalesId,
            ProjectName = "Issue Project"
        };
    }

    private static Order CreateOrder(TestIds ids)
    {
        return new Order
        {
            OrderId = ids.OrderId,
            ProjectId = ids.ProjectId,
            OrderCode = "ORD-001"
        };
    }

    private static OrderItem CreateOrderItem(TestIds ids)
    {
        return new OrderItem
        {
            OrderItemId = ids.OrderItemId,
            OrderId = ids.OrderId,
            ProductNameSnapshot = "Oak Chair",
            DeliveredQuantity = 2
        };
    }

    private static DeliveryProductIssueReport CreateIssue(TestIds ids)
    {
        return new DeliveryProductIssueReport
        {
            DeliveryProductIssueReportId = ids.IssueId,
            ProjectId = ids.ProjectId,
            OrderId = ids.OrderId,
            OrderItemId = ids.OrderItemId,
            IssueType = DeliveryProductIssueType.DAMAGED,
            Description = "Corner chipped",
            AffectedQuantity = 2,
            ReportedBy = ids.CustomerId,
            ReportedAt = DateTime.UtcNow
        };
    }

    private sealed record TestIds(
        Guid ProjectId,
        Guid SalesId,
        Guid ProductionId,
        Guid AdminId,
        Guid OrderId,
        Guid OrderItemId,
        Guid CustomerId,
        Guid IssueId);

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly TestIds _ids;

        public FakeProjectRepository(TestIds ids) => _ids = ids;

        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([_ids.AdminId]);

        public Task<Project?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<Project?>(null);

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<Infrastructure.ReadModels.Projects.ProjectDetailReadModel?> GetDetailAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Projects.ProjectDetailReadModel?>(null);

        public Task<Infrastructure.ReadModels.Projects.DesignerAccountReadModel?> GetActiveDesignerAsync(
            Guid designerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Projects.DesignerAccountReadModel?>(null);

        public Task<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectListItemReadModel>> GetListAsync(
            Infrastructure.ReadModels.Projects.ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectListItemReadModel>>([]);

        public Task<int> CountAsync(
            Infrastructure.ReadModels.Projects.ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectByUserItemReadModel>> GetByUserAsync(
            Infrastructure.ReadModels.Projects.ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectByUserItemReadModel>>([]);

        public Task<int> CountByUserAsync(
            Infrastructure.ReadModels.Projects.ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<Infrastructure.ReadModels.Projects.ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Projects.ProjectSearchIndexItemReadModel?>(null);

        public Task<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Projects.ProjectSearchIndexItemReadModel>>([]);

        public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>([]);
        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Project entity) { }
        public void Remove(Project entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeProductionRepository : IProductionRequestRepository
    {
        public IReadOnlyList<Guid> OrderAssignees { get; init; } = [];

        public Task<IReadOnlyList<Guid>> GetDistinctAssignedProductionAccountIdsForOrderAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(OrderAssignees);

        public Task<ProductionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ProductionRequest?>(null);
        public Task<bool> HasActiveRequestForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsOrderProductionCompletedAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<int> CountCreatedOnAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<List<OrderItem>> GetProductOrderItemsAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(new List<OrderItem>());
        public Task AddItemsAsync(List<ProductionItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsActiveProductionStaffAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Infrastructure.ReadModels.Production.ProductionAssigneeReadModel?> GetAssigneeAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<Infrastructure.ReadModels.Production.ProductionAssigneeReadModel?>(null);
        public Task<List<Infrastructure.ReadModels.Production.AvailableProductionStaffReadModel>> GetAvailableStaffAsync(string? search, CancellationToken cancellationToken = default) => Task.FromResult(new List<Infrastructure.ReadModels.Production.AvailableProductionStaffReadModel>());
        public Task<List<Infrastructure.ReadModels.Production.ProductionRequestListItemReadModel>> GetQueueAsync(Infrastructure.ReadModels.Production.ProductionRequestQueueReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(new List<Infrastructure.ReadModels.Production.ProductionRequestListItemReadModel>());
        public Task<bool> HasViewableAssignedRequestAsync(Guid projectId, Guid productionAccountId, CancellationToken cancellationToken = default) => Task.FromResult(false);
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

    private sealed class CapturingDispatcher : INotificationDispatcher
    {
        public List<CapturedDispatch> Dispatches { get; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            NotificationDispatchRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            Dispatches.Add(new CapturedDispatch(
                type,
                parameters,
                receiverIds.ToList(),
                request?.ReferenceType,
                request?.ReferenceId,
                NotificationTemplateProvider.Get(type).SignalREventName,
                request?.Metadata));
            return Task.CompletedTask;
        }
    }

    private sealed record CapturedDispatch(
        NotificationType Type,
        IReadOnlyDictionary<string, string> Parameters,
        List<Guid> Receivers,
        string? ReferenceType,
        Guid? ReferenceId,
        string? SignalREventName,
        IReadOnlyDictionary<string, object?>? Metadata);
}
