#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Reports;
using FurniSpace.Application.Services.Reports;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.ReadModels.Reports;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Reports;

public sealed class AdminProjectReportServiceCoverageTests
{
    [Fact]
    public async Task GetListAsync_NullQuery_UsesDefaults()
    {
        var service = CreateService(BuildCandidate(ProjectStatus.SUBMITTED, attention: true));
        var result = await service.GetListAsync(null!);
        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
    }

    [Theory]
    [InlineData("CRITICAL", null, null, null)]
    [InlineData(null, "BOSS", null, null)]
    [InlineData(null, null, "NOT_A_REASON", null)]
    [InlineData(null, null, null, -1)]
    public async Task GetListAsync_InvalidFilters_ReturnBadRequest(
        string? severity,
        string? ownerRole,
        string? attentionReason,
        int? minAgeDays)
    {
        var service = CreateService();
        var result = await service.GetListAsync(new AdminProjectReportsQueryDto
        {
            Severity = severity,
            OwnerRole = ownerRole,
            AttentionReason = attentionReason,
            MinAgeDays = minAgeDays
        });
        Assert.Equal(400, result.Status);
        Assert.Equal(AdminProjectReportErrorCodes.FilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetListAsync_InvalidStage_ReturnsBadRequest()
    {
        var service = CreateService();
        var result = await service.GetListAsync(new AdminProjectReportsQueryDto { Stage = "UNKNOWN_STAGE" });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetListAsync_FromAfterTo_ReturnsBadRequest()
    {
        var service = CreateService();
        var result = await service.GetListAsync(new AdminProjectReportsQueryDto
        {
            From = DateTime.UtcNow,
            To = DateTime.UtcNow.AddDays(-1)
        });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetListAsync_PageSizeOutOfRange_ReturnsBadRequest()
    {
        var service = CreateService();
        var result = await service.GetListAsync(new AdminProjectReportsQueryDto { PageSize = 101 });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetListAsync_AppliesFiltersSortPagingAndAttentionOnlyFalse()
    {
        var salesId = Guid.NewGuid();
        var old = BuildCandidate(
            ProjectStatus.SUBMITTED,
            attention: true,
            projectId: Guid.NewGuid(),
            submittedAt: DateTime.UtcNow.AddDays(-20),
            salesId: null);
        var waiting = BuildCandidate(
            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            attention: true,
            projectId: Guid.NewGuid(),
            submittedAt: DateTime.UtcNow.AddDays(-2),
            salesId: salesId,
            startFee: PaymentStatus.PAID);
        var healthy = BuildCandidate(
            ProjectStatus.PROPOSAL_SELECTED,
            attention: false,
            projectId: Guid.NewGuid(),
            submittedAt: DateTime.UtcNow.AddDays(-1),
            salesId: salesId,
            startFee: PaymentStatus.PAID);

        var service = CreateService(old, waiting, healthy);

        var filtered = await service.GetListAsync(new AdminProjectReportsQueryDto
        {
            AttentionOnly = true,
            AttentionReason = "WAITING_DESIGNER",
            Severity = "ACTION",
            OwnerRole = "ADMIN",
            MinAgeDays = 1,
            Stage = "DESIGNER_ASSIGNMENT",
            SortBy = "ageDaysDesc",
            Page = 1,
            PageSize = 10
        });
        Assert.Equal(200, filtered.Status);
        Assert.All(filtered.Data!.Items, i => Assert.Equal("WAITING_DESIGNER", i.AttentionReason));

        var all = await service.GetListAsync(new AdminProjectReportsQueryDto
        {
            AttentionOnly = false,
            SortBy = "submittedAtAsc",
            SortDirection = "asc",
            Page = 1,
            PageSize = 2
        });
        Assert.Equal(200, all.Status);
        Assert.Equal(3, all.Data!.TotalItems);
        Assert.Equal(2, all.Data.Items.Count);

        var bySubmittedDesc = await service.GetListAsync(new AdminProjectReportsQueryDto
        {
            AttentionOnly = false,
            SortBy = "submittedAtDesc",
            Page = 1,
            PageSize = 10
        });
        Assert.True(bySubmittedDesc.Data!.Items[0].SubmittedAt >= bySubmittedDesc.Data.Items[^1].SubmittedAt);

        var severitySort = await service.GetListAsync(new AdminProjectReportsQueryDto
        {
            AttentionOnly = true,
            SortBy = "severityDesc",
            Page = 1,
            PageSize = 10
        });
        Assert.NotEmpty(severitySort.Data!.Items);
    }

    [Fact]
    public async Task GetListAsync_SkipsWhenFilterDoesNotMatchPrimary()
    {
        var service = CreateService(BuildCandidate(ProjectStatus.SUBMITTED, attention: true));
        var result = await service.GetListAsync(new AdminProjectReportsQueryDto
        {
            AttentionOnly = true,
            AttentionReason = "PRODUCTION_BLOCKED"
        });
        Assert.Equal(200, result.Status);
        Assert.Empty(result.Data!.Items);
    }

    [Fact]
    public async Task GetDetailAsync_Completed_ReturnsTerminalSummary_AndNullStageHealth()
    {
        var projectId = Guid.NewGuid();
        var candidate = BuildCandidate(
            ProjectStatus.COMPLETED,
            attention: false,
            projectId: projectId,
            salesId: Guid.NewGuid(),
            completedAt: DateTime.UtcNow.AddDays(-1));
        var service = CreateService(detail: candidate, financial: new AdminFinancialProjectRowReadModel());

        var result = await service.GetDetailAsync(projectId);

        Assert.Equal(200, result.Status);
        Assert.Null(result.Data!.CurrentStageHealth);
        Assert.Equal("COMPLETED", result.Data.TerminalSummary!.Outcome);
        Assert.NotNull(result.Data.TerminalSummary.DurationDays);
        Assert.All(result.Data.FlowProgress.Stages, s => Assert.NotNull(s.Key));
    }

    [Fact]
    public async Task GetDetailAsync_Rejected_ReturnsTerminalSummary()
    {
        var projectId = Guid.NewGuid();
        var candidate = BuildCandidate(
            ProjectStatus.REJECTED,
            attention: false,
            projectId: projectId,
            salesId: Guid.NewGuid(),
            rejectedAt: DateTime.UtcNow.AddDays(-1),
            rejectionReason: "Out of scope");
        var service = CreateService(detail: candidate, financial: new AdminFinancialProjectRowReadModel());

        var result = await service.GetDetailAsync(projectId);

        Assert.Equal(200, result.Status);
        Assert.True(result.Data!.Header.IsRejected);
        Assert.Equal("REJECTED", result.Data.TerminalSummary!.Outcome);
        Assert.Equal("Out of scope", result.Data.TerminalSummary.RejectionReason);
        Assert.Null(result.Data.CurrentStageHealth);
    }

    [Fact]
    public async Task GetDetailAsync_ProductionBlocked_BuildsLinksAndBlockers()
    {
        var projectId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var productionId = Guid.NewGuid();
        var candidate = new AdminProjectReportCandidateReadModel
        {
            ProjectId = projectId,
            ProjectCode = "PRJ-P",
            ProjectName = "Prod",
            Status = ProjectStatus.IN_PRODUCTION,
            BusinessType = "Cafe",
            ProjectAddress = "Q1",
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            AssignedSalesId = Guid.NewGuid(),
            AssignedSalesName = "S",
            AssignedDesignerId = Guid.NewGuid(),
            AssignedDesignerName = "D",
            SubmittedAt = DateTime.UtcNow.AddDays(-20),
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            CancelledProductionItemCount = 2,
            LatestQuotationId = quotationId,
            LatestOrderId = orderId,
            LatestProductionRequestId = productionId,
            ProjectStartFeeStatus = PaymentStatus.PAID
        };
        var financial = new AdminFinancialProjectRowReadModel
        {
            ProjectId = projectId,
            ProjectStartFeeAmount = 2_000_000m,
            ProjectStartFeeStatus = PaymentStatus.PAID,
            OrderId = orderId,
            OrderCode = "ORD-P",
            OrderStatus = OrderStatus.IN_PRODUCTION,
            OrderFinalTotal = 50m,
            OrderPaidAmount = 15m,
            OrderRemainingAmount = 35m,
            TotalProjectCashCollected = 17m,
            LastPaidAt = DateTime.UtcNow.AddDays(-5)
        };
        var service = CreateService(detail: candidate, financial: financial);

        var result = await service.GetDetailAsync(projectId);

        Assert.Equal(200, result.Status);
        Assert.Equal("PRODUCTION_BLOCKED", result.Data!.Header.PrimaryAttention!.Reason);
        Assert.Equal("BLOCKED", result.Data.CurrentStageHealth!.State);
        Assert.NotEmpty(result.Data.CurrentStageHealth.Blockers);
        Assert.Contains(result.Data.CurrentStageHealth.Links, l => l.Type == "WORKFLOW");
        Assert.Contains(result.Data.CurrentStageHealth.Links, l => l.Type == "QUOTATION");
        Assert.Contains(result.Data.CurrentStageHealth.Links, l => l.Type == "ORDER");
        Assert.Contains(result.Data.CurrentStageHealth.Links, l => l.Type == "PRODUCTION_REQUEST");
        Assert.Equal("ORD-P", result.Data.CommercialSnapshot.OrderCode);
        Assert.Contains(
            result.Data.FlowProgress.Stages,
            s => s.Key == "PRODUCTION" && s.State is "BLOCKED" or "ACTIVE");
    }

    [Fact]
    public async Task GetDetailAsync_WithoutFinancialRow_ReturnsEmptyCommercialSnapshot()
    {
        var projectId = Guid.NewGuid();
        var candidate = BuildCandidate(
            ProjectStatus.SPACE_VERIFIED,
            attention: false,
            projectId: projectId,
            salesId: Guid.NewGuid(),
            startFee: PaymentStatus.PAID);
        var service = CreateService(detail: candidate, financial: null);

        var result = await service.GetDetailAsync(projectId);

        Assert.Equal(200, result.Status);
        Assert.Equal(0m, result.Data!.CommercialSnapshot.TotalProjectCashCollected);
        Assert.Null(result.Data.CommercialSnapshot.OrderId);
        Assert.NotNull(result.Data.CurrentStageHealth);
    }

    [Fact]
    public async Task GetDetailAsync_NeedBasicInformation_UsesBlockedStageCopy()
    {
        var projectId = Guid.NewGuid();
        var candidate = BuildCandidate(
            ProjectStatus.NEED_BASIC_INFORMATION,
            attention: true,
            projectId: projectId,
            salesId: Guid.NewGuid(),
            startFee: PaymentStatus.PAID,
            updatedAt: DateTime.UtcNow.AddDays(-5));
        var service = CreateService(detail: candidate, financial: new AdminFinancialProjectRowReadModel());

        var result = await service.GetDetailAsync(projectId);

        Assert.Equal(200, result.Status);
        Assert.Equal("BLOCKED", result.Data!.CurrentStageHealth!.State);
        Assert.Contains("information", result.Data.CurrentStageHealth.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminProjectReportService CreateService(
        params AdminProjectReportCandidateReadModel[] candidates)
    {
        return new AdminProjectReportService(
            new FakeRepo(candidates, detail: null),
            new FakeFinancial(new AdminFinancialProjectRowReadModel()));
    }

    private static AdminProjectReportService CreateService(
        AdminProjectReportCandidateReadModel? detail,
        AdminFinancialProjectRowReadModel? financial)
    {
        return new AdminProjectReportService(
            new FakeRepo([], detail),
            new FakeFinancial(financial));
    }

    private static AdminProjectReportCandidateReadModel BuildCandidate(
        ProjectStatus status,
        bool attention,
        Guid? projectId = null,
        Guid? salesId = null,
        PaymentStatus? startFee = null,
        DateTime? submittedAt = null,
        DateTime? completedAt = null,
        DateTime? rejectedAt = null,
        DateTime? updatedAt = null,
        string? rejectionReason = null)
    {
        // attention flag is informational for test authors; actual attention comes from status rules.
        _ = attention;
        return new AdminProjectReportCandidateReadModel
        {
            ProjectId = projectId ?? Guid.NewGuid(),
            ProjectCode = "PRJ-X",
            ProjectName = "Project",
            Status = status,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Customer",
            AssignedSalesId = salesId,
            AssignedSalesName = salesId is null ? null : "Sales",
            ProjectStartFeeStatus = startFee,
            SubmittedAt = submittedAt ?? DateTime.UtcNow.AddDays(-5),
            CreatedAt = submittedAt ?? DateTime.UtcNow.AddDays(-5),
            UpdatedAt = updatedAt ?? DateTime.UtcNow.AddDays(-1),
            CompletedAt = completedAt,
            RejectedAt = rejectedAt,
            RejectionReason = rejectionReason
        };
    }

    private sealed class FakeRepo : IAdminProjectReportRepository
    {
        private readonly IReadOnlyList<AdminProjectReportCandidateReadModel> _candidates;
        private readonly AdminProjectReportCandidateReadModel? _detail;

        public FakeRepo(
            IReadOnlyList<AdminProjectReportCandidateReadModel> candidates,
            AdminProjectReportCandidateReadModel? detail)
        {
            _candidates = candidates;
            _detail = detail;
        }

        public Task<IReadOnlyList<AdminProjectReportCandidateReadModel>> GetCandidatesAsync(
            AdminProjectReportListQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_candidates);

        public Task<AdminProjectReportCandidateReadModel?> GetCandidateAsync(
            Guid projectId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            if (_detail is not null && _detail.ProjectId == projectId)
            {
                return Task.FromResult<AdminProjectReportCandidateReadModel?>(_detail);
            }

            return Task.FromResult(_candidates.FirstOrDefault(c => c.ProjectId == projectId));
        }
    }

    private sealed class FakeFinancial : IFinancialReadRepository
    {
        private readonly AdminFinancialProjectRowReadModel? _row;

        public FakeFinancial(AdminFinancialProjectRowReadModel? row) => _row = row;

        public Task<AdminFinancialSummaryReadModel> GetAdminSummaryAsync(
            DateTime fromUtc, DateTime toUtcExclusive, DateTime utcNow, string currency,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminFinancialSummaryReadModel());

        public Task<AdminFinancialReceivablesSummaryReadModel> GetReceivablesSummaryAsync(
            AdminFinancialReceivablesQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminFinancialReceivablesSummaryReadModel());

        public Task<IReadOnlyList<AdminFinancialReceivableItemReadModel>> GetReceivableItemsAsync(
            AdminFinancialReceivablesQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialReceivableItemReadModel>>([]);

        public Task<int> CountReceivableItemsAsync(
            AdminFinancialReceivablesQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>> GetPaymentBreakdownAsync(
            DateTime fromUtc, DateTime toUtcExclusive, DateTime utcNow, string currency,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>>([]);

        public Task<IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>> GetCollectedAmountsByPaymentTypeAsync(
            DateTime fromUtc, DateTime toUtcExclusive, string currency,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>>([]);

        public Task<IReadOnlyList<AdminFinancialProjectRowReadModel>> GetProjectFinancialRowsAsync(
            AdminFinancialProjectsQueryReadModel query, DateTime utcNow,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialProjectRowReadModel>>([]);

        public Task<int> CountProjectFinancialRowsAsync(
            AdminFinancialProjectsQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AdminFinancialProjectRowReadModel?> GetProjectFinancialRowAsync(
            Guid projectId, DateTime utcNow,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_row);

        public Task<IReadOnlyList<AdminFinancialPaymentRowReadModel>> GetOperationalPaymentsAsync(
            AdminFinancialPaymentsQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialPaymentRowReadModel>>([]);

        public Task<int> CountOperationalPaymentsAsync(
            AdminFinancialPaymentsQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<AdminFinancialExceptionRowReadModel>> GetFinancialExceptionsAsync(
            AdminFinancialExceptionsQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialExceptionRowReadModel>>([]);

        public Task<int> CountFinancialExceptionsAsync(
            AdminFinancialExceptionsQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AdminFinancialSummaryDrilldownReadModel> GetSummaryDrilldownAsync(
            AdminFinancialSummaryDrilldownQueryReadModel query,
            DateTime fromUtc,
            DateTime toUtcExclusive,
            DateTime utcNow,
            string currency,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminFinancialSummaryDrilldownReadModel
            {
                Metric = query.Metric,
                Page = query.Page,
                PageSize = query.PageSize
            });
    }
}
