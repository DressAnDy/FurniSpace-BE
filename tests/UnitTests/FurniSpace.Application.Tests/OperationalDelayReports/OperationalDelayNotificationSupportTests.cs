#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.OperationalDelayReports;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurniSpace.Application.Tests.OperationalDelayReports;

public sealed class OperationalDelayNotificationSupportTests
{
    [Fact]
    public async Task TryDispatchProductionDelayReportedAsync_NotifiesSalesProductionAndAdmins()
    {
        var ids = CreateIds();
        var dispatcher = new CapturingDispatcher();
        var report = CreateReport(ids, OperationalDelayPhase.PRODUCTION);
        var project = CreateProject(ids);
        var productionRequest = CreateProductionRequest(ids);

        await OperationalDelayNotificationSupport.TryDispatchProductionDelayReportedAsync(
            dispatcher,
            new FakeProjectRepository(ids),
            NullLogger.Instance,
            report,
            project,
            productionRequest);

        var dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(NotificationType.ProductionDelayReported, dispatch.Type);
        Assert.Contains(ids.SalesId, dispatch.Receivers);
        Assert.Contains(ids.ProductionId, dispatch.Receivers);
        Assert.Contains(ids.AdminId, dispatch.Receivers);
        Assert.Equal(OperationalDelayNotificationSupport.ReferenceType, dispatch.ReferenceType);
        Assert.Equal(report.OperationalDelayReportId, dispatch.ReferenceId);
        Assert.Equal("production.delay.reported", dispatch.SignalREventName);
        Assert.Equal(ids.ProductionRequestId, dispatch.Metadata!["productionRequestId"]);
    }

    [Fact]
    public async Task TryDispatchDeliveryDelayReportedAsync_ResolvesProjectProductionAssignees()
    {
        var ids = CreateIds();
        var dispatcher = new CapturingDispatcher();
        var report = CreateReport(ids, OperationalDelayPhase.DELIVERY);
        var project = CreateProject(ids);
        var productionRequests = new FakeProductionRepository
        {
            ProjectAssignees = [ids.ProductionId, ids.SecondProductionId]
        };

        await OperationalDelayNotificationSupport.TryDispatchDeliveryDelayReportedAsync(
            dispatcher,
            new FakeProjectRepository(ids),
            productionRequests,
            NullLogger.Instance,
            report,
            project);

        var dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(NotificationType.DeliveryDelayReported, dispatch.Type);
        Assert.Contains(ids.ProductionId, dispatch.Receivers);
        Assert.Contains(ids.SecondProductionId, dispatch.Receivers);
        Assert.Equal("delivery.delay.reported", dispatch.SignalREventName);
        Assert.Equal("DELIVERY", dispatch.Metadata!["reportPhase"]);
    }

    [Fact]
    public async Task TryDispatchProductionDelayReportedAsync_WhenDispatcherThrows_DoesNotThrow()
    {
        var ids = CreateIds();
        var report = CreateReport(ids, OperationalDelayPhase.PRODUCTION);

        var exception = await Record.ExceptionAsync(() =>
            OperationalDelayNotificationSupport.TryDispatchProductionDelayReportedAsync(
                new ThrowingDispatcher(),
                new FakeProjectRepository(ids),
                NullLogger.Instance,
                report,
                CreateProject(ids),
                CreateProductionRequest(ids)));

        Assert.Null(exception);
    }

    [Fact]
    public async Task BuildStaffRecipientsAsync_ExcludesEmptyGuids()
    {
        var ids = CreateIds();
        var receivers = await OperationalExceptionNotificationRecipientSupport.BuildStaffRecipientsAsync(
            new FakeProjectRepository(ids),
            assignedSalesId: Guid.Empty,
            [Guid.Empty, ids.ProductionId],
            CancellationToken.None);

        Assert.Contains(ids.ProductionId, receivers);
        Assert.Contains(ids.AdminId, receivers);
        Assert.DoesNotContain(Guid.Empty, receivers);
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
            AssignedSalesId = ids.SalesId,
            ProjectName = "Delay Project"
        };
    }

    private static ProductionRequest CreateProductionRequest(TestIds ids)
    {
        return new ProductionRequest
        {
            ProductionRequestId = ids.ProductionRequestId,
            ProjectId = ids.ProjectId,
            AssignedTo = ids.ProductionId
        };
    }

    private static OperationalDelayReport CreateReport(TestIds ids, OperationalDelayPhase phase)
    {
        return new OperationalDelayReport
        {
            OperationalDelayReportId = ids.ReportId,
            ProjectId = ids.ProjectId,
            ReportPhase = phase,
            ProductionRequestId = phase == OperationalDelayPhase.PRODUCTION ? ids.ProductionRequestId : null,
            DelayState = OperationalDelayState.AT_RISK,
            ProductionReasonCode = phase == OperationalDelayPhase.PRODUCTION
                ? ProductionDelayReasonCode.MATERIAL_DELAY
                : null,
            DeliveryReasonCode = phase == OperationalDelayPhase.DELIVERY
                ? DeliveryDelayReasonCode.PRODUCT_NOT_READY
                : null,
            ReasonDetail = "Delay detail",
            ReportedBy = ids.SalesId,
            ReportedAt = DateTime.UtcNow
        };
    }

    private sealed record TestIds(
        Guid ProjectId,
        Guid SalesId,
        Guid ProductionId,
        Guid SecondProductionId,
        Guid AdminId,
        Guid ProductionRequestId,
        Guid ReportId);

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
        public IReadOnlyList<Guid> ProjectAssignees { get; init; } = [];
        public IReadOnlyList<Guid> OrderAssignees { get; init; } = [];

        public Task<IReadOnlyList<Guid>> GetDistinctAssignedProductionAccountIdsForProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ProjectAssignees);

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
                receiverIds.ToList(),
                request?.ReferenceType,
                request?.ReferenceId,
                NotificationTemplateProvider.Get(type).SignalREventName,
                request?.Metadata));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDispatcher : INotificationDispatcher
    {
        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            NotificationDispatchRequest? request = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("dispatch failed");
    }

    private sealed record CapturedDispatch(
        NotificationType Type,
        List<Guid> Receivers,
        string? ReferenceType,
        Guid? ReferenceId,
        string? SignalREventName,
        IReadOnlyDictionary<string, object?>? Metadata);
}
