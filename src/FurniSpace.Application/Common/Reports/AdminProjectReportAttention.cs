#nullable enable

using FurniSpace.Application.Common.Projects;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Reports;

namespace FurniSpace.Application.Common.Reports;

internal static class AdminProjectReportAttention
{
    public const string UnassignedIntake = "UNASSIGNED_INTAKE";
    public const string WaitingCustomerInfo = "WAITING_CUSTOMER_INFO";
    public const string StartFeeBlocking = "START_FEE_BLOCKING";
    public const string WaitingDesigner = "WAITING_DESIGNER";
    public const string MeasurementOverdue = "MEASUREMENT_OVERDUE";
    public const string ProposalStalled = "PROPOSAL_STALLED";
    public const string QuotationRevisionLoop = "QUOTATION_REVISION_LOOP";
    public const string PaymentException = "PAYMENT_EXCEPTION";
    public const string ProductionBlocked = "PRODUCTION_BLOCKED";
    public const string DeliveryOverdue = "DELIVERY_OVERDUE";
    public const string FinalPaymentPending = "FINAL_PAYMENT_PENDING";
    public const string ReadyToComplete = "READY_TO_COMPLETE";

    public const string SeverityWatch = "WATCH";
    public const string SeverityAction = "ACTION";
    public const string SeverityEscalate = "ESCALATE";

    public const string RoleSales = "SALES";
    public const string RoleDesigner = "DESIGNER";
    public const string RoleProduction = "PRODUCTION";
    public const string RoleAdmin = "ADMIN";

    public const int CustomerInfoWatchDays = 3;
    public const int CustomerInfoActionDays = 7;
    public const int ProposalStallWatchDays = 7;
    public const int ProposalStallActionDays = 14;
    public const int QuotationRevisionLoopDays = 7;
    public const int QuotationRevisionLoopCount = 2;
    public const int PaymentStuckDays = 3;

    private static readonly string[] PriorityOrder =
    [
        ProductionBlocked,
        PaymentException,
        DeliveryOverdue,
        StartFeeBlocking,
        UnassignedIntake,
        WaitingDesigner,
        MeasurementOverdue,
        QuotationRevisionLoop,
        FinalPaymentPending,
        WaitingCustomerInfo,
        ProposalStalled,
        ReadyToComplete
    ];

    public sealed record AttentionHit(
        string Reason,
        string Severity,
        string OwnerRole,
        string SuggestedAction);

    public static IReadOnlyList<AttentionHit> Evaluate(
        AdminProjectReportCandidateReadModel project,
        DateTime utcNow,
        int ageInStatusDays)
    {
        var hits = new List<AttentionHit>();
        TryAddUnassignedIntake(project, hits);
        TryAddWaitingCustomerInfo(project, ageInStatusDays, hits);
        TryAddStartFeeBlocking(project, hits);
        TryAddWaitingDesigner(project, hits);
        TryAddMeasurementOverdue(project, hits);
        TryAddProposalStalled(project, ageInStatusDays, hits);
        TryAddQuotationRevisionLoop(project, ageInStatusDays, hits);
        TryAddPaymentException(project, utcNow, hits);
        TryAddProductionBlocked(project, hits);
        TryAddDeliveryOverdue(project, hits);
        TryAddFinalPaymentPending(project, hits);
        TryAddReadyToComplete(project, hits);

        return hits
            .OrderBy(h => Array.IndexOf(PriorityOrder, h.Reason))
            .ThenByDescending(h => SeverityRank(h.Severity))
            .ToList();
    }

    public static AttentionHit? Primary(IReadOnlyList<AttentionHit> hits) =>
        hits.Count == 0 ? null : hits[0];

    public static string? ResolveStageKey(ProjectStatus? status)
    {
        var index = ProjectWorkflowStageCatalog.ResolveStageIndex(status);
        return index is null ? null : ProjectWorkflowStageCatalog.Stages[index.Value].Key;
    }

    public static int AgeDays(DateTime? submittedAt, DateTime? createdAt, DateTime utcNow)
    {
        var start = submittedAt ?? createdAt;
        if (start is null)
        {
            return 0;
        }

        var days = (utcNow.Date - start.Value.Date).Days;
        return Math.Max(0, days);
    }

    public static DateTime EstimateStatusEnteredAt(AdminProjectReportCandidateReadModel project)
    {
        return project.Status switch
        {
            ProjectStatus.SUBMITTED => project.SubmittedAt ?? project.CreatedAt ?? UtcFallback(project),
            ProjectStatus.IN_CONSULTATION => project.SalesAssignedAt ?? project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.NEED_BASIC_INFORMATION => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT => project.ApprovedAt ?? project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.MEASUREMENT_REQUIRED => project.DesignerAssignedAt ?? project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.SPACE_VERIFIED => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.PROPOSAL_CONSULTING => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.PROPOSAL_SELECTED => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.QUOTATION_SENT => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.QUOTATION_REVISION_REQUESTED => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.ORDER_CONFIRMED => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.IN_PRODUCTION => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.READY_FOR_DELIVERY => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.DELIVERING => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.DELIVERED => project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.COMPLETED => project.CompletedAt ?? project.UpdatedAt ?? UtcFallback(project),
            ProjectStatus.REJECTED => project.RejectedAt ?? project.UpdatedAt ?? UtcFallback(project),
            _ => project.UpdatedAt ?? project.CreatedAt ?? DateTime.UtcNow
        };
    }

    public static int AgeInStatusDays(AdminProjectReportCandidateReadModel project, DateTime utcNow)
    {
        var entered = EstimateStatusEnteredAt(project);
        return Math.Max(0, (utcNow.Date - entered.Date).Days);
    }

    public static int AgeInStageDays(AdminProjectReportCandidateReadModel project, DateTime utcNow) =>
        AgeInStatusDays(project, utcNow);

    public static int SeverityRank(string severity) => severity switch
    {
        SeverityEscalate => 3,
        SeverityAction => 2,
        SeverityWatch => 1,
        _ => 0
    };

    private static void TryAddUnassignedIntake(
        AdminProjectReportCandidateReadModel project,
        List<AttentionHit> hits)
    {
        if (project.AssignedSalesId is not null)
        {
            return;
        }

        if (project.Status is not (ProjectStatus.SUBMITTED or ProjectStatus.IN_CONSULTATION))
        {
            return;
        }

        hits.Add(new AttentionHit(
            UnassignedIntake,
            SeverityAction,
            RoleAdmin,
            "Assign Sales and move project into consultation."));
    }

    private static void TryAddWaitingCustomerInfo(
        AdminProjectReportCandidateReadModel project,
        int ageInStatusDays,
        List<AttentionHit> hits)
    {
        if (project.Status != ProjectStatus.NEED_BASIC_INFORMATION
            || ageInStatusDays < CustomerInfoWatchDays)
        {
            return;
        }

        hits.Add(new AttentionHit(
            WaitingCustomerInfo,
            ageInStatusDays >= CustomerInfoActionDays ? SeverityAction : SeverityWatch,
            RoleSales,
            "Follow up with customer for missing basic information."));
    }

    private static void TryAddStartFeeBlocking(
        AdminProjectReportCandidateReadModel project,
        List<AttentionHit> hits)
    {
        if (!IsStartFeeBlocking(project))
        {
            return;
        }

        hits.Add(new AttentionHit(
            StartFeeBlocking,
            SeverityAction,
            RoleSales,
            "Follow up Project Start Fee payment before designer assignment."));
    }

    private static void TryAddWaitingDesigner(
        AdminProjectReportCandidateReadModel project,
        List<AttentionHit> hits)
    {
        if (project.Status != ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT)
        {
            return;
        }

        hits.Add(new AttentionHit(
            WaitingDesigner,
            SeverityAction,
            RoleAdmin,
            "Assign an available designer to the project."));
    }

    private static void TryAddMeasurementOverdue(
        AdminProjectReportCandidateReadModel project,
        List<AttentionHit> hits)
    {
        if (project.Status != ProjectStatus.MEASUREMENT_REQUIRED
            || !project.HasOverdueMeasurementSchedule)
        {
            return;
        }

        hits.Add(new AttentionHit(
            MeasurementOverdue,
            SeverityAction,
            RoleDesigner,
            "Reschedule or complete overdue measurement."));
    }

    private static void TryAddProposalStalled(
        AdminProjectReportCandidateReadModel project,
        int ageInStatusDays,
        List<AttentionHit> hits)
    {
        if (project.Status != ProjectStatus.PROPOSAL_CONSULTING
            || ageInStatusDays < ProposalStallWatchDays)
        {
            return;
        }

        hits.Add(new AttentionHit(
            ProposalStalled,
            ageInStatusDays >= ProposalStallActionDays ? SeverityAction : SeverityWatch,
            RoleDesigner,
            "Follow up proposal consulting with customer."));
    }

    private static void TryAddQuotationRevisionLoop(
        AdminProjectReportCandidateReadModel project,
        int ageInStatusDays,
        List<AttentionHit> hits)
    {
        if (project.Status != ProjectStatus.QUOTATION_REVISION_REQUESTED)
        {
            return;
        }

        if (project.QuotationRevisionRequestedCount < QuotationRevisionLoopCount
            && ageInStatusDays < QuotationRevisionLoopDays)
        {
            return;
        }

        hits.Add(new AttentionHit(
            QuotationRevisionLoop,
            SeverityAction,
            RoleSales,
            "Break the quotation revision loop and align scope/price."));
    }

    private static void TryAddPaymentException(
        AdminProjectReportCandidateReadModel project,
        DateTime utcNow,
        List<AttentionHit> hits)
    {
        if (!IsPaymentException(project, utcNow))
        {
            return;
        }

        hits.Add(new AttentionHit(
            PaymentException,
            project.HasExpiredCollectiblePayment ? SeverityEscalate : SeverityAction,
            RoleSales,
            "Resolve expired or stuck collectible payment."));
    }

    private static void TryAddProductionBlocked(
        AdminProjectReportCandidateReadModel project,
        List<AttentionHit> hits)
    {
        if (project.Status != ProjectStatus.IN_PRODUCTION
            || project.CancelledProductionItemCount <= 0)
        {
            return;
        }

        hits.Add(new AttentionHit(
            ProductionBlocked,
            SeverityEscalate,
            RoleProduction,
            "Open production request and clear blocked/cancelled items."));
    }

    private static void TryAddDeliveryOverdue(
        AdminProjectReportCandidateReadModel project,
        List<AttentionHit> hits)
    {
        if (!project.HasOverdueDeliverySchedule)
        {
            return;
        }

        if (project.Status is not (ProjectStatus.READY_FOR_DELIVERY or ProjectStatus.DELIVERING))
        {
            return;
        }

        hits.Add(new AttentionHit(
            DeliveryOverdue,
            SeverityAction,
            RoleSales,
            "Resolve overdue delivery or handover schedule."));
    }

    private static void TryAddFinalPaymentPending(
        AdminProjectReportCandidateReadModel project,
        List<AttentionHit> hits)
    {
        if (!IsFinalPaymentPending(project))
        {
            return;
        }

        hits.Add(new AttentionHit(
            FinalPaymentPending,
            SeverityAction,
            RoleSales,
            "Collect remaining payment after delivery."));
    }

    private static void TryAddReadyToComplete(
        AdminProjectReportCandidateReadModel project,
        List<AttentionHit> hits)
    {
        if (!IsReadyToComplete(project))
        {
            return;
        }

        hits.Add(new AttentionHit(
            ReadyToComplete,
            SeverityWatch,
            RoleSales,
            "Explicitly complete order and project."));
    }

    private static bool IsStartFeeBlocking(AdminProjectReportCandidateReadModel project)
    {
        if (project.AssignedSalesId is null)
        {
            return false;
        }

        if (project.Status is not (ProjectStatus.IN_CONSULTATION or ProjectStatus.NEED_BASIC_INFORMATION))
        {
            return false;
        }

        return project.ProjectStartFeeStatus is not PaymentStatus.PAID;
    }

    private static bool IsPaymentException(AdminProjectReportCandidateReadModel project, DateTime utcNow)
    {
        if (project.HasExpiredCollectiblePayment)
        {
            return true;
        }

        if (project.ActivePaymentCreatedAt is null)
        {
            return false;
        }

        var stuckDays = (utcNow.Date - project.ActivePaymentCreatedAt.Value.Date).Days;
        return stuckDays >= PaymentStuckDays
               && project.ActivePaymentStatus is PaymentStatus.PENDING or PaymentStatus.PROCESSING;
    }

    private static bool IsFinalPaymentPending(AdminProjectReportCandidateReadModel project)
    {
        if (project.Status != ProjectStatus.DELIVERED)
        {
            return false;
        }

        if (project.LatestOrderStatus == OrderStatus.FINAL_PAYMENT_PENDING)
        {
            return true;
        }

        return project.LatestOrderRemainingAmount is > 0;
    }

    private static bool IsReadyToComplete(AdminProjectReportCandidateReadModel project)
    {
        if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.REJECTED)
        {
            return false;
        }

        if (project.Status != ProjectStatus.DELIVERED)
        {
            return false;
        }

        if (project.LatestOrderStatus == OrderStatus.COMPLETED)
        {
            return true;
        }

        var remaining = project.LatestOrderRemainingAmount ?? 0m;
        return remaining <= 0m
               && project.LatestOrderStatus is OrderStatus.DELIVERED or OrderStatus.FINAL_PAYMENT_PENDING;
    }

    private static DateTime UtcFallback(AdminProjectReportCandidateReadModel project) =>
        project.CreatedAt ?? DateTime.UtcNow;
}
