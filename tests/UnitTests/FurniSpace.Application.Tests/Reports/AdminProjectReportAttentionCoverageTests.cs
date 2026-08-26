#nullable enable

using System;
using System.Linq;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Common.Reports;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Reports;
using Xunit;

namespace FurniSpace.Application.Tests.Reports;

public sealed class AdminProjectReportAttentionCoverageTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_UnassignedIntake_OnSubmittedAndInConsultation()
    {
        var submitted = Candidate(status: ProjectStatus.SUBMITTED);
        var consulting = Candidate(status: ProjectStatus.IN_CONSULTATION);

        Assert.Contains(AdminProjectReportAttention.Evaluate(submitted, Now, 1),
            h => h.Reason == AdminProjectReportAttention.UnassignedIntake);
        Assert.Contains(AdminProjectReportAttention.Evaluate(consulting, Now, 1),
            h => h.Reason == AdminProjectReportAttention.UnassignedIntake);
    }

    [Theory]
    [InlineData(3, AdminProjectReportAttention.SeverityWatch)]
    [InlineData(7, AdminProjectReportAttention.SeverityAction)]
    public void Evaluate_WaitingCustomerInfo_SeverityByAge(int ageDays, string severity)
    {
        var project = Candidate(
            status: ProjectStatus.NEED_BASIC_INFORMATION,
            salesId: Guid.NewGuid(),
            startFeeStatus: PaymentStatus.PAID);

        var hit = AdminProjectReportAttention.Evaluate(project, Now, ageDays)
            .Single(h => h.Reason == AdminProjectReportAttention.WaitingCustomerInfo);

        Assert.Equal(severity, hit.Severity);
        Assert.Equal(AdminProjectReportAttention.RoleSales, hit.OwnerRole);
    }

    [Fact]
    public void Evaluate_WaitingCustomerInfo_BelowWatchThreshold_Skipped()
    {
        var project = Candidate(
            status: ProjectStatus.NEED_BASIC_INFORMATION,
            salesId: Guid.NewGuid(),
            startFeeStatus: PaymentStatus.PAID);

        Assert.DoesNotContain(
            AdminProjectReportAttention.Evaluate(project, Now, 2),
            h => h.Reason == AdminProjectReportAttention.WaitingCustomerInfo);
    }

    [Fact]
    public void Evaluate_StartFeeBlocking_WhenUnpaidInConsultation()
    {
        var project = Candidate(
            status: ProjectStatus.IN_CONSULTATION,
            salesId: Guid.NewGuid(),
            startFeeStatus: PaymentStatus.PENDING);

        Assert.Contains(
            AdminProjectReportAttention.Evaluate(project, Now, 1),
            h => h.Reason == AdminProjectReportAttention.StartFeeBlocking);
    }

    [Fact]
    public void Evaluate_StartFeeBlocking_SkippedWithoutSalesOrPaidFee()
    {
        var noSales = Candidate(status: ProjectStatus.IN_CONSULTATION, startFeeStatus: null);
        var paid = Candidate(
            status: ProjectStatus.IN_CONSULTATION,
            salesId: Guid.NewGuid(),
            startFeeStatus: PaymentStatus.PAID);

        Assert.DoesNotContain(
            AdminProjectReportAttention.Evaluate(noSales, Now, 1),
            h => h.Reason == AdminProjectReportAttention.StartFeeBlocking);
        Assert.DoesNotContain(
            AdminProjectReportAttention.Evaluate(paid, Now, 1),
            h => h.Reason == AdminProjectReportAttention.StartFeeBlocking);
    }

    [Fact]
    public void Evaluate_WaitingDesigner()
    {
        var project = Candidate(
            status: ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            salesId: Guid.NewGuid(),
            startFeeStatus: PaymentStatus.PAID);

        var primary = AdminProjectReportAttention.Primary(
            AdminProjectReportAttention.Evaluate(project, Now, 2));

        Assert.NotNull(primary);
        Assert.Equal(AdminProjectReportAttention.WaitingDesigner, primary!.Reason);
    }

    [Fact]
    public void Evaluate_MeasurementOverdue()
    {
        var project = Candidate(
            status: ProjectStatus.MEASUREMENT_REQUIRED,
            salesId: Guid.NewGuid(),
            designerId: Guid.NewGuid(),
            overdueMeasurement: true);

        Assert.Contains(
            AdminProjectReportAttention.Evaluate(project, Now, 1),
            h => h.Reason == AdminProjectReportAttention.MeasurementOverdue
                 && h.OwnerRole == AdminProjectReportAttention.RoleDesigner);
    }

    [Theory]
    [InlineData(7, AdminProjectReportAttention.SeverityWatch)]
    [InlineData(14, AdminProjectReportAttention.SeverityAction)]
    public void Evaluate_ProposalStalled(int ageDays, string severity)
    {
        var project = Candidate(status: ProjectStatus.PROPOSAL_CONSULTING, salesId: Guid.NewGuid());
        var hit = AdminProjectReportAttention.Evaluate(project, Now, ageDays)
            .Single(h => h.Reason == AdminProjectReportAttention.ProposalStalled);
        Assert.Equal(severity, hit.Severity);
    }

    [Fact]
    public void Evaluate_QuotationRevisionLoop_ByCountOrAge()
    {
        var byCount = Candidate(
            status: ProjectStatus.QUOTATION_REVISION_REQUESTED,
            salesId: Guid.NewGuid(),
            revisionCount: 2);
        var byAge = Candidate(
            status: ProjectStatus.QUOTATION_REVISION_REQUESTED,
            salesId: Guid.NewGuid(),
            revisionCount: 1);

        Assert.Contains(
            AdminProjectReportAttention.Evaluate(byCount, Now, 1),
            h => h.Reason == AdminProjectReportAttention.QuotationRevisionLoop);
        Assert.Contains(
            AdminProjectReportAttention.Evaluate(byAge, Now, 7),
            h => h.Reason == AdminProjectReportAttention.QuotationRevisionLoop);
    }

    [Fact]
    public void Evaluate_PaymentException_ExpiredEscalate_AndStuckAction()
    {
        var expired = Candidate(
            status: ProjectStatus.QUOTATION_SENT,
            salesId: Guid.NewGuid(),
            expiredPayment: true);
        var stuck = Candidate(
            status: ProjectStatus.ORDER_CONFIRMED,
            salesId: Guid.NewGuid(),
            activePaymentCreatedAt: Now.AddDays(-3),
            activePaymentStatus: PaymentStatus.PENDING);

        var expiredHit = AdminProjectReportAttention.Evaluate(expired, Now, 1)
            .Single(h => h.Reason == AdminProjectReportAttention.PaymentException);
        var stuckHit = AdminProjectReportAttention.Evaluate(stuck, Now, 1)
            .Single(h => h.Reason == AdminProjectReportAttention.PaymentException);

        Assert.Equal(AdminProjectReportAttention.SeverityEscalate, expiredHit.Severity);
        Assert.Equal(AdminProjectReportAttention.SeverityAction, stuckHit.Severity);
    }

    [Fact]
    public void Evaluate_ProductionBlocked()
    {
        var project = Candidate(
            status: ProjectStatus.IN_PRODUCTION,
            salesId: Guid.NewGuid(),
            cancelledProductionItems: 2);

        var primary = AdminProjectReportAttention.Primary(
            AdminProjectReportAttention.Evaluate(project, Now, 1));

        Assert.Equal(AdminProjectReportAttention.ProductionBlocked, primary!.Reason);
        Assert.Equal(AdminProjectReportAttention.SeverityEscalate, primary.Severity);
    }

    [Theory]
    [InlineData(ProjectStatus.READY_FOR_DELIVERY)]
    [InlineData(ProjectStatus.DELIVERING)]
    public void Evaluate_DeliveryOverdue(ProjectStatus status)
    {
        var project = Candidate(status: status, salesId: Guid.NewGuid(), overdueDelivery: true);
        Assert.Contains(
            AdminProjectReportAttention.Evaluate(project, Now, 1),
            h => h.Reason == AdminProjectReportAttention.DeliveryOverdue);
    }

    [Fact]
    public void Evaluate_FinalPaymentPending_ByOrderStatusOrRemaining()
    {
        var byStatus = Candidate(
            status: ProjectStatus.DELIVERED,
            salesId: Guid.NewGuid(),
            orderStatus: OrderStatus.FINAL_PAYMENT_PENDING,
            remaining: 0m);
        var byRemaining = Candidate(
            status: ProjectStatus.DELIVERED,
            salesId: Guid.NewGuid(),
            orderStatus: OrderStatus.DELIVERED,
            remaining: 10m);

        Assert.Contains(
            AdminProjectReportAttention.Evaluate(byStatus, Now, 1),
            h => h.Reason == AdminProjectReportAttention.FinalPaymentPending);
        Assert.Contains(
            AdminProjectReportAttention.Evaluate(byRemaining, Now, 1),
            h => h.Reason == AdminProjectReportAttention.FinalPaymentPending);
    }

    [Fact]
    public void Evaluate_ReadyToComplete_WhenOrderCompletedOrNoRemaining()
    {
        var orderCompleted = Candidate(
            status: ProjectStatus.DELIVERED,
            salesId: Guid.NewGuid(),
            orderStatus: OrderStatus.COMPLETED,
            remaining: 0m);
        var deliveredPaid = Candidate(
            status: ProjectStatus.DELIVERED,
            salesId: Guid.NewGuid(),
            orderStatus: OrderStatus.DELIVERED,
            remaining: 0m);

        Assert.Contains(
            AdminProjectReportAttention.Evaluate(orderCompleted, Now, 1),
            h => h.Reason == AdminProjectReportAttention.ReadyToComplete);
        Assert.Contains(
            AdminProjectReportAttention.Evaluate(deliveredPaid, Now, 1),
            h => h.Reason == AdminProjectReportAttention.ReadyToComplete);
    }

    [Fact]
    public void Evaluate_ReadyToComplete_SkippedForTerminalProjects()
    {
        var completed = Candidate(status: ProjectStatus.COMPLETED, salesId: Guid.NewGuid());
        var rejected = Candidate(status: ProjectStatus.REJECTED, salesId: Guid.NewGuid());

        Assert.Empty(AdminProjectReportAttention.Evaluate(completed, Now, 1));
        Assert.Empty(AdminProjectReportAttention.Evaluate(rejected, Now, 1));
    }

    [Fact]
    public void Primary_ReturnsNullWhenEmpty_AndFirstWhenPresent()
    {
        Assert.Null(AdminProjectReportAttention.Primary([]));
        var hits = AdminProjectReportAttention.Evaluate(
            Candidate(status: ProjectStatus.SUBMITTED), Now, 1);
        Assert.Same(hits[0], AdminProjectReportAttention.Primary(hits));
    }

    [Fact]
    public void ResolveStageKey_MapsKnownStatuses_AndNullForRejected()
    {
        Assert.Equal(ProjectWorkflowStageCatalog.StageIntake,
            AdminProjectReportAttention.ResolveStageKey(ProjectStatus.SUBMITTED));
        Assert.Equal(ProjectWorkflowStageCatalog.StageProduction,
            AdminProjectReportAttention.ResolveStageKey(ProjectStatus.IN_PRODUCTION));
        Assert.Null(AdminProjectReportAttention.ResolveStageKey(ProjectStatus.REJECTED));
        Assert.Null(AdminProjectReportAttention.ResolveStageKey(null));
    }

    [Fact]
    public void AgeDays_UsesSubmittedOrCreated_OrZero()
    {
        Assert.Equal(0, AdminProjectReportAttention.AgeDays(null, null, Now));
        Assert.Equal(5, AdminProjectReportAttention.AgeDays(Now.AddDays(-5), null, Now));
        Assert.Equal(3, AdminProjectReportAttention.AgeDays(null, Now.AddDays(-3), Now));
    }

    [Fact]
    public void EstimateStatusEnteredAt_CoversAllStatuses()
    {
        foreach (ProjectStatus status in Enum.GetValues<ProjectStatus>())
        {
            var project = Candidate(
                status: status,
                submittedAt: Now.AddDays(-20),
                salesAssignedAt: Now.AddDays(-19),
                approvedAt: Now.AddDays(-18),
                designerAssignedAt: Now.AddDays(-17),
                completedAt: Now.AddDays(-1),
                rejectedAt: Now.AddDays(-1),
                updatedAt: Now.AddDays(-2),
                createdAt: Now.AddDays(-21));

            var entered = AdminProjectReportAttention.EstimateStatusEnteredAt(project);
            Assert.True(entered <= Now);
            Assert.True(AdminProjectReportAttention.AgeInStatusDays(project, Now) >= 0);
            Assert.Equal(
                AdminProjectReportAttention.AgeInStatusDays(project, Now),
                AdminProjectReportAttention.AgeInStageDays(project, Now));
        }
    }

    [Fact]
    public void SeverityRank_KnownAndUnknown()
    {
        Assert.Equal(3, AdminProjectReportAttention.SeverityRank(AdminProjectReportAttention.SeverityEscalate));
        Assert.Equal(2, AdminProjectReportAttention.SeverityRank(AdminProjectReportAttention.SeverityAction));
        Assert.Equal(1, AdminProjectReportAttention.SeverityRank(AdminProjectReportAttention.SeverityWatch));
        Assert.Equal(0, AdminProjectReportAttention.SeverityRank("OTHER"));
    }

    private static AdminProjectReportCandidateReadModel Candidate(
        ProjectStatus status,
        Guid? salesId = null,
        Guid? designerId = null,
        PaymentStatus? startFeeStatus = null,
        bool overdueMeasurement = false,
        bool overdueDelivery = false,
        bool expiredPayment = false,
        DateTime? activePaymentCreatedAt = null,
        PaymentStatus? activePaymentStatus = null,
        int revisionCount = 0,
        int cancelledProductionItems = 0,
        OrderStatus? orderStatus = null,
        decimal? remaining = null,
        DateTime? submittedAt = null,
        DateTime? salesAssignedAt = null,
        DateTime? approvedAt = null,
        DateTime? designerAssignedAt = null,
        DateTime? completedAt = null,
        DateTime? rejectedAt = null,
        DateTime? updatedAt = null,
        DateTime? createdAt = null)
    {
        return new AdminProjectReportCandidateReadModel
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "PRJ-COV",
            ProjectName = "Coverage",
            Status = status,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Customer",
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            ProjectStartFeeStatus = startFeeStatus,
            HasOverdueMeasurementSchedule = overdueMeasurement,
            HasOverdueDeliverySchedule = overdueDelivery,
            HasExpiredCollectiblePayment = expiredPayment,
            ActivePaymentCreatedAt = activePaymentCreatedAt,
            ActivePaymentStatus = activePaymentStatus,
            QuotationRevisionRequestedCount = revisionCount,
            CancelledProductionItemCount = cancelledProductionItems,
            LatestOrderStatus = orderStatus,
            LatestOrderRemainingAmount = remaining,
            SubmittedAt = submittedAt ?? Now.AddDays(-10),
            SalesAssignedAt = salesAssignedAt,
            ApprovedAt = approvedAt,
            DesignerAssignedAt = designerAssignedAt,
            CompletedAt = completedAt,
            RejectedAt = rejectedAt,
            UpdatedAt = updatedAt ?? Now.AddDays(-1),
            CreatedAt = createdAt ?? Now.AddDays(-10)
        };
    }
}
