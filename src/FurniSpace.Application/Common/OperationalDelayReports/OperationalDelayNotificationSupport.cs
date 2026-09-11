using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Common.OperationalDelayReports;

internal static class OperationalDelayNotificationSupport
{
    internal const string ReferenceType = "OPERATIONAL_DELAY_REPORT";

    internal static Task TryDispatchProductionDelayReportedAsync(
        INotificationDispatcher? notifications,
        IProjectRepository projects,
        ILogger? logger,
        OperationalDelayReport report,
        Project project,
        ProductionRequest productionRequest,
        CancellationToken cancellationToken = default)
    {
        var productionAccountIds = productionRequest.AssignedTo.HasValue
            ? new[] { productionRequest.AssignedTo.Value }
            : Array.Empty<Guid>();

        return TryDispatchAsync(
            notifications,
            projects,
            logger,
            NotificationType.ProductionDelayReported,
            report,
            project,
            productionAccountIds,
            BuildProductionMetadata(report),
            cancellationToken);
    }

    internal static async Task TryDispatchDeliveryDelayReportedAsync(
        INotificationDispatcher? notifications,
        IProjectRepository projects,
        IProductionRequestRepository productionRequests,
        ILogger? logger,
        OperationalDelayReport report,
        Project project,
        CancellationToken cancellationToken = default)
    {
        var productionAccountIds = await productionRequests
            .GetDistinctAssignedProductionAccountIdsForProjectAsync(project.ProjectId, cancellationToken);

        await TryDispatchAsync(
            notifications,
            projects,
            logger,
            NotificationType.DeliveryDelayReported,
            report,
            project,
            productionAccountIds,
            BuildDeliveryMetadata(report),
            cancellationToken);
    }

    private static async Task TryDispatchAsync(
        INotificationDispatcher? notifications,
        IProjectRepository projects,
        ILogger? logger,
        NotificationType type,
        OperationalDelayReport report,
        Project project,
        IReadOnlyList<Guid> productionAccountIds,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken)
    {
        if (notifications is null)
        {
            return;
        }

        var receivers = await OperationalExceptionNotificationRecipientSupport
            .BuildStaffRecipientsAsync(projects, project.AssignedSalesId, productionAccountIds, cancellationToken);
        if (receivers.Count == 0)
        {
            return;
        }

        var parameters = new Dictionary<string, string>
        {
            ["ProjectName"] = project.ProjectName,
            ["DelayState"] = report.DelayState.ToString(),
            ["ReasonCode"] = ResolveReasonCode(report)
        };

        try
        {
            await notifications.DispatchAsync(
                type,
                parameters,
                receivers,
                new NotificationDispatchRequest(
                    project.ProjectId,
                    ReferenceType,
                    report.OperationalDelayReportId,
                    metadata),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                exception,
                "Failed to dispatch operational delay notification {NotificationType} for report {ReportId}",
                type,
                report.OperationalDelayReportId);
        }
    }

    private static string ResolveReasonCode(OperationalDelayReport report)
    {
        return report.ProductionReasonCode?.ToString()
            ?? report.DeliveryReasonCode?.ToString()
            ?? string.Empty;
    }

    private static Dictionary<string, object?> BuildProductionMetadata(OperationalDelayReport report)
    {
        return new Dictionary<string, object?>
        {
            ["operationalDelayReportId"] = report.OperationalDelayReportId,
            ["reportPhase"] = report.ReportPhase.ToString(),
            ["delayState"] = report.DelayState.ToString(),
            ["productionReasonCode"] = report.ProductionReasonCode?.ToString(),
            ["productionRequestId"] = report.ProductionRequestId
        };
    }

    private static Dictionary<string, object?> BuildDeliveryMetadata(OperationalDelayReport report)
    {
        return new Dictionary<string, object?>
        {
            ["operationalDelayReportId"] = report.OperationalDelayReportId,
            ["reportPhase"] = report.ReportPhase.ToString(),
            ["delayState"] = report.DelayState.ToString(),
            ["deliveryReasonCode"] = report.DeliveryReasonCode?.ToString()
        };
    }
}

internal static class OperationalExceptionNotificationRecipientSupport
{
    internal static async Task<List<Guid>> BuildStaffRecipientsAsync(
        IProjectRepository projects,
        Guid? assignedSalesId,
        IReadOnlyList<Guid> productionAccountIds,
        CancellationToken cancellationToken)
    {
        var receivers = new HashSet<Guid>();
        if (assignedSalesId.HasValue && assignedSalesId.Value != Guid.Empty)
        {
            receivers.Add(assignedSalesId.Value);
        }

        foreach (var productionAccountId in productionAccountIds)
        {
            if (productionAccountId != Guid.Empty)
            {
                receivers.Add(productionAccountId);
            }
        }

        var adminIds = await projects.GetActiveAccountIdsByRoleNamesAsync(
            [ApplicationRoles.Admin],
            cancellationToken);
        foreach (var adminId in adminIds)
        {
            if (adminId != Guid.Empty)
            {
                receivers.Add(adminId);
            }
        }

        return [.. receivers];
    }
}
