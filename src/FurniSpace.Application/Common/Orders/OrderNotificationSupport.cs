using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.Orders;

internal static class OrderNotificationSupport
{
    internal const string OrderReferenceType = "ORDER";
    internal const string OrderCodeParameter = "OrderCode";
    internal const string StatusParameter = "Status";

    internal static Task TryDispatchUpdatedAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        Order order,
        Project project,
        CancellationToken cancellationToken = default)
    {
        return TryDispatchToCustomerAndSalesAsync(
            notifications,
            logger,
            NotificationType.OrderUpdated,
            order,
            project,
            cancellationToken);
    }

    internal static Task TryDispatchDeliveredAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        Order order,
        Project project,
        CancellationToken cancellationToken = default)
    {
        return TryDispatchToCustomerAndSalesAsync(
            notifications,
            logger,
            NotificationType.OrderDelivered,
            order,
            project,
            cancellationToken);
    }

    internal static Task TryDispatchCompletedAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        Order order,
        Project project,
        CancellationToken cancellationToken = default)
    {
        return TryDispatchToCustomerAndSalesAsync(
            notifications,
            logger,
            NotificationType.OrderCompleted,
            order,
            project,
            cancellationToken);
    }

    internal static Task TryDispatchItemDeliveryUpdatedAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        Order order,
        Project project,
        OrderItem orderItem,
        CancellationToken cancellationToken = default)
    {
        return TryDispatchAsync(
            notifications,
            logger,
            new OrderDispatchCall(
                NotificationType.OrderItemDeliveryUpdated,
                order,
                project,
                [project.CustomerId],
                BuildOrderItemMetadata(order, orderItem)),
            cancellationToken);
    }

    internal static Task TryDispatchItemDeliveryConfirmedAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        Order order,
        Project project,
        OrderItem orderItem,
        CancellationToken cancellationToken = default)
    {
        if (project.AssignedSalesId is null)
        {
            return Task.CompletedTask;
        }

        return TryDispatchAsync(
            notifications,
            logger,
            new OrderDispatchCall(
                NotificationType.OrderItemDeliveryConfirmed,
                order,
                project,
                [project.AssignedSalesId.Value],
                BuildOrderItemMetadata(order, orderItem)),
            cancellationToken);
    }

    internal static Task TryDispatchProjectStatusChangedAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        Project project,
        IEnumerable<Guid> receiverIds,
        CancellationToken cancellationToken = default)
    {
        if (notifications is null)
        {
            return Task.CompletedTask;
        }

        var receivers = receiverIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (receivers.Count == 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            return notifications.DispatchAsync(
                NotificationType.ProjectStatusChanged,
                new Dictionary<string, string>
                {
                    ["ProjectName"] = project.ProjectName,
                    [StatusParameter] = project.Status?.ToString() ?? string.Empty
                },
                receivers,
                new NotificationDispatchRequest(
                    project.ProjectId,
                    "PROJECT",
                    project.ProjectId,
                    new Dictionary<string, object?>
                    {
                        ["newProjectStatus"] = project.Status?.ToString()
                    }),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                exception,
                "Failed to dispatch project status changed notification for project {ProjectId}",
                project.ProjectId);
            return Task.CompletedTask;
        }
    }

    private static Task TryDispatchToCustomerAndSalesAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        NotificationType type,
        Order order,
        Project project,
        CancellationToken cancellationToken)
    {
        var receivers = BuildCustomerAndSalesReceivers(order, project);
        return TryDispatchAsync(
            notifications,
            logger,
            new OrderDispatchCall(
                type,
                order,
                project,
                receivers,
                BuildOrderMetadata(order)),
            cancellationToken);
    }

    private static Task TryDispatchAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        OrderDispatchCall call,
        CancellationToken cancellationToken = default)
    {
        if (notifications is null || call.ReceiverIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            return notifications.DispatchAsync(
                call.Type,
                new Dictionary<string, string>
                {
                    [OrderCodeParameter] = call.Order.OrderCode,
                    [StatusParameter] = call.Order.Status?.ToString() ?? string.Empty
                },
                call.ReceiverIds,
                new NotificationDispatchRequest(
                    call.Project.ProjectId,
                    OrderReferenceType,
                    call.Order.OrderId,
                    call.Metadata),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                exception,
                "Failed to dispatch order notification {NotificationType} for order {OrderId}",
                call.Type,
                call.Order.OrderId);
            return Task.CompletedTask;
        }
    }

    internal static List<Guid> BuildCustomerAndSalesReceivers(Order order, Project project)
    {
        var receivers = new HashSet<Guid> { project.CustomerId };
        if (order.SalesId.HasValue)
        {
            receivers.Add(order.SalesId.Value);
        }
        else if (project.AssignedSalesId.HasValue)
        {
            receivers.Add(project.AssignedSalesId.Value);
        }

        return [.. receivers];
    }

    private static Dictionary<string, object?> BuildOrderMetadata(Order order)
    {
        return new Dictionary<string, object?>
        {
            ["orderId"] = order.OrderId,
            ["orderStatus"] = order.Status?.ToString()
        };
    }

    private static Dictionary<string, object?> BuildOrderItemMetadata(Order order, OrderItem orderItem)
    {
        var metadata = BuildOrderMetadata(order);
        metadata["orderItemId"] = orderItem.OrderItemId;
        return metadata;
    }

    private sealed record OrderDispatchCall(
        NotificationType Type,
        Order Order,
        Project Project,
        List<Guid> ReceiverIds,
        IReadOnlyDictionary<string, object?>? Metadata);
}
