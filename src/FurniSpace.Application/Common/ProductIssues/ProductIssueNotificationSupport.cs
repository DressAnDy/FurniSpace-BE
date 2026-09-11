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
        if (notifications is null)
        {
            return;
        }

        var productionAccountIds = await productionRequests
            .GetDistinctAssignedProductionAccountIdsForOrderAsync(order.OrderId, cancellationToken);
        var receivers = await OperationalExceptionNotificationRecipientSupport
            .BuildStaffRecipientsAsync(projects, project.AssignedSalesId, productionAccountIds, cancellationToken);
        if (receivers.Count == 0)
        {
            return;
        }

        var parameters = new Dictionary<string, string>
        {
            ["IssueType"] = issue.IssueType.ToString(),
            ["ProductName"] = orderItem.ProductNameSnapshot
                ?? orderItem.ProductVersionNameSnapshot
                ?? "Product",
            ["OrderCode"] = order.OrderCode
        };

        var metadata = new Dictionary<string, object?>
        {
            ["deliveryProductIssueReportId"] = issue.DeliveryProductIssueReportId,
            ["orderId"] = issue.OrderId,
            ["orderItemId"] = issue.OrderItemId,
            ["issueType"] = issue.IssueType.ToString(),
            ["affectedQuantity"] = issue.AffectedQuantity
        };

        try
        {
            await notifications.DispatchAsync(
                NotificationType.ProductIssueReported,
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
                "Failed to dispatch product issue notification for report {ReportId}",
                issue.DeliveryProductIssueReportId);
        }
    }
}
