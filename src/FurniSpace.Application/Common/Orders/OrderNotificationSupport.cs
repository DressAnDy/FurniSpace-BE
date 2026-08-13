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
            NotificationType.OrderItemDeliveryUpdated,
            order,
            project,
            [project.CustomerId],
            metadata: BuildOrderItemMetadata(order, orderItem),
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
            NotificationType.OrderItemDeliveryConfirmed,
            order,
            project,
            [project.AssignedSalesId.Value],
            metadata: BuildOrderItemMetadata(order, orderItem),
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
                projectId: project.ProjectId,
                referenceType: "PROJECT",
                referenceId: project.ProjectId,
                cancellationToken,
                metadata: new Dictionary<string, object?>
                {
                    ["newProjectStatus"] = project.Status?.ToString()
                });
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
            type,
            order,
            project,
            receivers,
            metadata: BuildOrderMetadata(order),
            cancellationToken);
    }

    private static Task TryDispatchAsync(
        INotificationDispatcher? notifications,
        ILogger? logger,
        NotificationType type,
        Order order,
        Project project,
        IReadOnlyCollection<Guid> receiverIds,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (notifications is null || receiverIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            return notifications.DispatchAsync(
                type,
                new Dictionary<string, string>
                {
                    [OrderCodeParameter] = order.OrderCode,
                    [StatusParameter] = order.Status?.ToString() ?? string.Empty
                },
                receiverIds,
                projectId: project.ProjectId,
                referenceType: OrderReferenceType,
                referenceId: order.OrderId,
                cancellationToken,
                metadata: metadata);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                exception,
                "Failed to dispatch order notification {NotificationType} for order {OrderId}",
                type,
                order.OrderId);
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
}
