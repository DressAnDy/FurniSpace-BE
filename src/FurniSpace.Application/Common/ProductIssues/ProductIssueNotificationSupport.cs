using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.OperationalDelayReports;
using FurniSpace.Application.Interfaces.Notifications;
using static FurniSpace.Application.Constants.ProductIssues.ProductIssueServiceConstants;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.ProductIssues;

internal static class ProductIssueNotificationSupport
{
    internal static async Task TryDispatchReportedAsync(
        INotificationDispatcher? notifications,
        IProjectRepository projects,
        IProductionRequestRepository productionRequests,
        ILogger? logger,
        DeliveryProductIssueReport issue,
        Project project,
        Order order,
        OrderItem orderItem,
        CancellationToken cancellationToken = default)
    {
        await TryDispatchAsync(
            notifications,
            projects,
            productionRequests,
            logger,
            NotificationType.ProductIssueReported,
            issue,
            project,
            order,
            orderItem,
            includeCustomer: true,
            cancellationToken);
    }

    internal static async Task TryDispatchResolvedAsync(
        INotificationDispatcher? notifications,
        IProjectRepository projects,
        IProductionRequestRepository productionRequests,
        ILogger? logger,
        DeliveryProductIssueReport issue,
        Project project,
        Order order,
        OrderItem orderItem,
        CancellationToken cancellationToken = default)
    {
        await TryDispatchAsync(
            notifications,
            projects,
            productionRequests,
            logger,
            NotificationType.ProductIssueResolved,
            issue,
            project,
            order,
            orderItem,
            includeCustomer: true,
            cancellationToken);
    }

    private static async Task TryDispatchAsync(
        INotificationDispatcher? notifications,
        IProjectRepository projects,
        IProductionRequestRepository productionRequests,
        ILogger? logger,
        NotificationType type,
        DeliveryProductIssueReport issue,
        Project project,
        Order order,
        OrderItem orderItem,
        bool includeCustomer,
        CancellationToken cancellationToken)
    {
        if (notifications is null)
        {
            return;
        }

        var productionAccountIds = await productionRequests
            .GetDistinctAssignedProductionAccountIdsForOrderAsync(order.OrderId, cancellationToken);
        var receivers = await OperationalExceptionNotificationRecipientSupport
            .BuildStaffRecipientsAsync(projects, project.AssignedSalesId, productionAccountIds, cancellationToken);

        if (includeCustomer && project.CustomerId != Guid.Empty)
        {
            receivers.Add(project.CustomerId);
        }

        if (receivers.Count == 0)
        {
            return;
        }

        var productName = orderItem.ProductNameSnapshot
            ?? orderItem.ProductVersionNameSnapshot
            ?? "Product";

        var parameters = new Dictionary<string, string>
        {
            ["IssueType"] = issue.IssueType.ToString(),
            ["ProductName"] = productName,
            ["OrderCode"] = order.OrderCode
        };

        var metadata = new Dictionary<string, object?>
        {
            ["projectId"] = project.ProjectId,
            ["orderId"] = issue.OrderId,
            ["issueId"] = issue.DeliveryProductIssueReportId,
            ["deliveryProductIssueReportId"] = issue.DeliveryProductIssueReportId,
            ["orderItemId"] = issue.OrderItemId,
            ["issueType"] = issue.IssueType.ToString(),
            ["affectedQuantity"] = issue.AffectedQuantity
        };

        try
        {
            await notifications.DispatchAsync(
                type,
                parameters,
                receivers,
                new NotificationDispatchRequest(
                    project.ProjectId,
                    IssueReportReferenceType,
                    issue.DeliveryProductIssueReportId,
                    metadata),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                exception,
                "Failed to dispatch product issue notification {NotificationType} for report {ReportId}",
                type,
                issue.DeliveryProductIssueReportId);
        }
    }
}
