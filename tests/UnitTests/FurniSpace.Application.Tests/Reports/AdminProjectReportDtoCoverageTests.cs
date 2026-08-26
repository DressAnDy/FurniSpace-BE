#nullable enable

using System;
using FurniSpace.Application.DTOs.Reports;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Reports;
using Xunit;

namespace FurniSpace.Application.Tests.Reports;

/// <summary>
/// Touches DTO / read-model property surfaces for Sonar coverage.
/// </summary>
public sealed class AdminProjectReportDtoCoverageTests
{
    [Fact]
    public void ApplicationDtos_ExposeAssignedProperties()
    {
        var query = new AdminProjectReportsQueryDto
        {
            Keyword = "cafe",
            Stage = "INTAKE",
            ProjectStatus = ProjectStatus.SUBMITTED,
            AttentionReason = "UNASSIGNED_INTAKE",
            Severity = "ACTION",
            OwnerRole = "ADMIN",
            SalesId = Guid.NewGuid(),
            DesignerId = Guid.NewGuid(),
            AttentionOnly = false,
            MinAgeDays = 2,
            From = DateTime.UtcNow.AddDays(-7),
            To = DateTime.UtcNow,
            Page = 2,
            PageSize = 10,
            SortBy = "ageDaysDesc",
            SortDirection = "asc"
        };
        Assert.Equal("cafe", query.Keyword);
        Assert.False(query.AttentionOnly);
        Assert.Equal(10, query.PageSize);

        var item = new AdminProjectReportListItemDto
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "PRJ-1",
            ProjectName = "Cafe",
            ProjectStatus = ProjectStatus.SUBMITTED,
            Stage = "INTAKE",
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            AssignedSalesId = Guid.NewGuid(),
            AssignedSalesName = "S",
            AssignedDesignerId = Guid.NewGuid(),
            AssignedDesignerName = "D",
            AgeDays = 5,
            AgeInStatusDays = 2,
            AttentionReason = "UNASSIGNED_INTAKE",
            SuggestedAction = "Assign",
            OwnerRole = "ADMIN",
            Severity = "ACTION",
            SubmittedAt = DateTime.UtcNow
        };
        Assert.Equal(5, item.AgeDays);

        var attention = new AdminProjectReportAttentionDto
        {
            Reason = "X",
            Severity = "WATCH",
            OwnerRole = "SALES",
            SuggestedAction = "Do"
        };
        var header = new AdminProjectReportHeaderDto
        {
            ProjectId = item.ProjectId,
            ProjectCode = "PRJ-1",
            ProjectName = "Cafe",
            ProjectStatus = ProjectStatus.SUBMITTED,
            Stage = "INTAKE",
            IsRejected = false,
            RejectionReason = null,
            BusinessType = "Cafe",
            ProjectAddress = "Q1",
            CustomerId = item.CustomerId,
            CustomerName = "C",
            AgeDays = 5,
            AgeInStatusDays = 2,
            PrimaryAttention = attention,
            AllAttentionReasons = ["X"]
        };
        Assert.Equal("Cafe", header.BusinessType);

        var health = new AdminProjectReportStageHealthDto
        {
            Stage = "INTAKE",
            State = "ACTIVE",
            StatusInStage = ProjectStatus.IN_CONSULTATION,
            Title = "Intake",
            Summary = "In progress",
            AgeInStageDays = 1,
            Blockers = [new AdminProjectReportBlockerDto { Code = "A", Message = "B" }],
            NextAction = new AdminProjectReportNextActionDto { OwnerRole = "SALES", SuggestedAction = "Go" },
            Links = [new AdminProjectReportLinkDto { Type = "ORDER", Id = Guid.NewGuid(), Label = "Order" }]
        };
        Assert.Single(health.Blockers);

        var flow = new AdminProjectReportFlowProgressDto
        {
            Stages =
            [
                new AdminProjectReportFlowStageDto
                {
                    Key = "INTAKE",
                    Label = "Intake",
                    State = "COMPLETED",
                    CompletedAt = DateTime.UtcNow
                }
            ]
        };
        Assert.Single(flow.Stages);

        var commercial = new AdminProjectReportCommercialSnapshotDto
        {
            ProjectStartFeeAmount = 1m,
            ProjectStartFeeStatus = PaymentStatus.PAID,
            ProjectStartFeePaidAt = DateTime.UtcNow,
            OrderId = Guid.NewGuid(),
            OrderCode = "ORD",
            OrderStatus = OrderStatus.CREATED,
            OrderFinalTotal = 2m,
            OrderPaidAmount = 1m,
            OrderRemainingAmount = 1m,
            ActivePaymentId = Guid.NewGuid(),
            ActivePaymentType = PaymentType.DEPOSIT,
            ActivePaymentAmount = 1m,
            ActivePaymentStatus = PaymentStatus.PENDING,
            TotalProjectCashCollected = 1m,
            LastPaidAt = DateTime.UtcNow
        };
        Assert.Equal(1m, commercial.TotalProjectCashCollected);

        var terminal = new AdminProjectReportTerminalSummaryDto
        {
            Outcome = "COMPLETED",
            CompletedAt = DateTime.UtcNow,
            RejectedAt = null,
            DurationDays = 10,
            RejectionReason = null,
            Note = "Done"
        };
        var detail = new AdminProjectReportDetailDto
        {
            Header = header,
            CurrentStageHealth = health,
            FlowProgress = flow,
            CommercialSnapshot = commercial,
            TerminalSummary = terminal
        };
        Assert.Equal("COMPLETED", detail.TerminalSummary!.Outcome);
        Assert.Equal(AdminProjectReportErrorCodes.FilterInvalid, "PROJECT_REPORT_FILTER_INVALID");
        Assert.Equal(AdminProjectReportErrorCodes.ProjectNotFound, "PROJECT_NOT_FOUND");
    }

    [Fact]
    public void ReadModels_ExposeAssignedProperties()
    {
        var query = new AdminProjectReportListQueryReadModel
        {
            Keyword = "x",
            StageStatuses = [ProjectStatus.SUBMITTED],
            ProjectStatus = ProjectStatus.SUBMITTED,
            SalesId = Guid.NewGuid(),
            DesignerId = Guid.NewGuid(),
            FromUtc = DateTime.UtcNow.AddDays(-1),
            ToUtcExclusive = DateTime.UtcNow,
            ExcludeTerminal = true
        };
        Assert.True(query.ExcludeTerminal);
        Assert.Single(query.StageStatuses!);

        var candidate = new AdminProjectReportCandidateReadModel
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "P",
            ProjectName = "N",
            Status = ProjectStatus.IN_PRODUCTION,
            BusinessType = "Cafe",
            ProjectAddress = "A",
            RejectionReason = null,
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            AssignedSalesId = Guid.NewGuid(),
            AssignedSalesName = "S",
            AssignedDesignerId = Guid.NewGuid(),
            AssignedDesignerName = "D",
            SubmittedAt = DateTime.UtcNow,
            SalesAssignedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow,
            DesignerAssignedAt = DateTime.UtcNow,
            CompletedAt = null,
            RejectedAt = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ProjectStartFeeStatus = PaymentStatus.PAID,
            ActivePaymentCreatedAt = DateTime.UtcNow,
            ActivePaymentStatus = PaymentStatus.PENDING,
            ActivePaymentType = PaymentType.DEPOSIT,
            HasExpiredCollectiblePayment = false,
            QuotationRevisionRequestedCount = 1,
            LatestQuotationId = Guid.NewGuid(),
            LatestOrderId = Guid.NewGuid(),
            LatestOrderStatus = OrderStatus.IN_PRODUCTION,
            LatestOrderRemainingAmount = 10m,
            LatestProductionRequestId = Guid.NewGuid(),
            CancelledProductionItemCount = 1,
            HasOverdueMeasurementSchedule = true,
            HasOverdueDeliverySchedule = true
        };
        Assert.Equal(1, candidate.CancelledProductionItemCount);
        Assert.True(candidate.HasOverdueDeliverySchedule);
    }
}
