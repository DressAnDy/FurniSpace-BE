#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderNotificationSupportTests
{
    [Fact]
    public void BuildCustomerAndSalesReceivers_PrefersOrderSalesThenProjectSales()
    {
        var customerId = Guid.NewGuid();
        var orderSalesId = Guid.NewGuid();
        var projectSalesId = Guid.NewGuid();
        var project = CreateProject(customerId, projectSalesId);

        var fromOrder = OrderNotificationSupport.BuildCustomerAndSalesReceivers(
            CreateOrder(customerId, orderSalesId),
            project);
        var fromProject = OrderNotificationSupport.BuildCustomerAndSalesReceivers(
            CreateOrder(customerId, salesId: null),
            project);

        Assert.Contains(customerId, fromOrder);
        Assert.Contains(orderSalesId, fromOrder);
        Assert.DoesNotContain(projectSalesId, fromOrder);
        Assert.Contains(customerId, fromProject);
        Assert.Contains(projectSalesId, fromProject);
    }

    [Fact]
    public async Task TryDispatchUpdatedAsync_WhenDispatcherMissing_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            OrderNotificationSupport.TryDispatchUpdatedAsync(
                notifications: null,
                logger: null,
                CreateOrder(Guid.NewGuid(), Guid.NewGuid()),
                CreateProject(Guid.NewGuid(), Guid.NewGuid())));

        Assert.Null(exception);
    }

    [Fact]
    public async Task TryDispatchUpdatedAndDeliveredAndCompleted_DispatchesToCustomerAndSales()
    {
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var dispatcher = new CapturingDispatcher();
        var order = CreateOrder(customerId, salesId);
        var project = CreateProject(customerId, salesId);

        await OrderNotificationSupport.TryDispatchUpdatedAsync(dispatcher, NullLogger.Instance, order, project);
        await OrderNotificationSupport.TryDispatchDeliveredAsync(dispatcher, NullLogger.Instance, order, project);
        await OrderNotificationSupport.TryDispatchCompletedAsync(dispatcher, NullLogger.Instance, order, project);

        Assert.Equal(
            [
                NotificationType.OrderUpdated,
                NotificationType.OrderDelivered,
                NotificationType.OrderCompleted
            ],
            dispatcher.Dispatches.Select(dispatch => dispatch.Type).ToArray());
        Assert.All(dispatcher.Dispatches, dispatch =>
        {
            Assert.Contains(customerId, dispatch.Receivers);
            Assert.Contains(salesId, dispatch.Receivers);
            Assert.Equal("ORDER", dispatch.ReferenceType);
            Assert.Equal(order.OrderId, dispatch.ReferenceId);
        });
    }

    [Fact]
    public async Task TryDispatchItemDeliveryUpdatedAsync_NotifiesCustomerOnly()
    {
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var dispatcher = new CapturingDispatcher();
        var order = CreateOrder(customerId, salesId);
        var item = CreateOrderItem(order.OrderId);

        await OrderNotificationSupport.TryDispatchItemDeliveryUpdatedAsync(
            dispatcher,
            NullLogger.Instance,
            order,
            CreateProject(customerId, salesId),
            item);

        var dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(NotificationType.OrderItemDeliveryUpdated, dispatch.Type);
        Assert.Equal(customerId, Assert.Single(dispatch.Receivers));
        Assert.Equal(item.OrderItemId, dispatch.Metadata!["orderItemId"]);
    }

    [Fact]
    public async Task TryDispatchItemDeliveryConfirmedAsync_WhenSalesMissing_Skips()
    {
        var dispatcher = new CapturingDispatcher();
        var customerId = Guid.NewGuid();

        await OrderNotificationSupport.TryDispatchItemDeliveryConfirmedAsync(
            dispatcher,
            NullLogger.Instance,
            CreateOrder(customerId, salesId: null),
            CreateProject(customerId, assignedSalesId: null),
            CreateOrderItem(Guid.NewGuid()));

        Assert.Empty(dispatcher.Dispatches);
    }

    [Fact]
    public async Task TryDispatchItemDeliveryConfirmedAsync_NotifiesAssignedSales()
    {
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var dispatcher = new CapturingDispatcher();
        var order = CreateOrder(customerId, salesId);
        var item = CreateOrderItem(order.OrderId);

        await OrderNotificationSupport.TryDispatchItemDeliveryConfirmedAsync(
            dispatcher,
            NullLogger.Instance,
            order,
            CreateProject(customerId, salesId),
            item);

        var dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(NotificationType.OrderItemDeliveryConfirmed, dispatch.Type);
        Assert.Equal(salesId, Assert.Single(dispatch.Receivers));
    }

    [Fact]
    public async Task TryDispatchProjectStatusChangedAsync_SkipsEmptyReceiversAndSwallowsExceptions()
    {
        var dispatcher = new CapturingDispatcher();
        var project = CreateProject(Guid.NewGuid(), Guid.NewGuid());

        await OrderNotificationSupport.TryDispatchProjectStatusChangedAsync(
            dispatcher,
            NullLogger.Instance,
            project,
            [Guid.Empty]);
        Assert.Empty(dispatcher.Dispatches);

        var exception = await Record.ExceptionAsync(() =>
            OrderNotificationSupport.TryDispatchProjectStatusChangedAsync(
                new ThrowingDispatcher(),
                NullLogger.Instance,
                project,
                [Guid.NewGuid()]));

        Assert.Null(exception);
    }

    [Fact]
    public async Task TryDispatchProjectStatusChangedAsync_DispatchesMetadata()
    {
        var receiverId = Guid.NewGuid();
        var dispatcher = new CapturingDispatcher();
        var project = CreateProject(Guid.NewGuid(), Guid.NewGuid());
        project.Status = ProjectStatus.DELIVERING;

        await OrderNotificationSupport.TryDispatchProjectStatusChangedAsync(
            dispatcher,
            NullLogger.Instance,
            project,
            [receiverId]);

        var dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(NotificationType.ProjectStatusChanged, dispatch.Type);
        Assert.Equal("PROJECT", dispatch.ReferenceType);
        Assert.Equal(project.ProjectId, dispatch.ReferenceId);
        Assert.Equal("DELIVERING", dispatch.Metadata!["newProjectStatus"]);
    }

    [Fact]
    public async Task TryDispatchUpdatedAsync_WhenDispatcherThrows_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            OrderNotificationSupport.TryDispatchUpdatedAsync(
                new ThrowingDispatcher(),
                NullLogger.Instance,
                CreateOrder(Guid.NewGuid(), Guid.NewGuid()),
                CreateProject(Guid.NewGuid(), Guid.NewGuid())));

        Assert.Null(exception);
    }

    private static Order CreateOrder(Guid customerId, Guid? salesId)
    {
        return new Order
        {
            OrderId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CustomerId = customerId,
            SalesId = salesId,
            OrderCode = "ORD-001",
            Status = OrderStatus.DELIVERING
        };
    }

    private static Project CreateProject(Guid customerId, Guid? assignedSalesId)
    {
        return new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = customerId,
            AssignedSalesId = assignedSalesId,
            ProjectName = "Cafe",
            Status = ProjectStatus.DELIVERING
        };
    }

    private static OrderItem CreateOrderItem(Guid orderId)
    {
        return new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Quantity = 2,
            Status = OrderItemStatus.READY
        };
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
        {
            throw new InvalidOperationException("Dispatch failed.");
        }
    }

    private sealed record CapturedDispatch(
        NotificationType Type,
        IReadOnlyList<Guid> Receivers,
        string? ReferenceType,
        Guid? ReferenceId,
        IReadOnlyDictionary<string, object?>? Metadata);
}
