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

public sealed class AdminProjectReportServiceTests
{
    [Fact]
    public async Task GetListAsync_AttentionOnly_ReturnsUnassignedIntake()
    {
        var projectId = Guid.NewGuid();
        var repository = new FakeAdminProjectReportRepository
        {
            Candidates =
            [
                new AdminProjectReportCandidateReadModel
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-1",
                    ProjectName = "Cafe",
                    Status = ProjectStatus.SUBMITTED,
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "Customer A",
                    SubmittedAt = DateTime.UtcNow.AddDays(-5),
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                }
            ]
        };
        var service = new AdminProjectReportService(repository, new FakeFinancialReadRepository());

        var result = await service.GetListAsync(new AdminProjectReportsQueryDto
        {
            AttentionOnly = true,
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Equal("UNASSIGNED_INTAKE", result.Data.Items[0].AttentionReason);
        Assert.Equal("ACTION", result.Data.Items[0].Severity);
        Assert.Equal("INTAKE", result.Data.Items[0].Stage);
    }

    [Fact]
    public async Task GetListAsync_InvalidSeverity_ReturnsBadRequest()
    {
        var service = new AdminProjectReportService(
            new FakeAdminProjectReportRepository(),
            new FakeFinancialReadRepository());

        var result = await service.GetListAsync(new AdminProjectReportsQueryDto
        {
            Severity = "CRITICAL"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminProjectReportErrorCodes.FilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetDetailAsync_WhenMissing_ReturnsNotFound()
    {
        var service = new AdminProjectReportService(
            new FakeAdminProjectReportRepository(),
            new FakeFinancialReadRepository());

        var result = await service.GetDetailAsync(Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(AdminProjectReportErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsFourBlocksAndCommercialSnapshot()
    {
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var repository = new FakeAdminProjectReportRepository
        {
            Detail = new AdminProjectReportCandidateReadModel
            {
                ProjectId = projectId,
                ProjectCode = "PRJ-2",
                ProjectName = "Shop",
                Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
                CustomerId = Guid.NewGuid(),
                CustomerName = "Customer B",
                AssignedSalesId = Guid.NewGuid(),
                AssignedSalesName = "Sales A",
                SubmittedAt = DateTime.UtcNow.AddDays(-10),
                SalesAssignedAt = DateTime.UtcNow.AddDays(-9),
                ApprovedAt = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                ProjectStartFeeStatus = PaymentStatus.PAID,
                LatestOrderId = orderId
            }
        };
        var financial = new FakeFinancialReadRepository
        {
            ProjectRow = new AdminFinancialProjectRowReadModel
            {
                ProjectId = projectId,
                ProjectStartFeeAmount = 2_000_000m,
                ProjectStartFeeStatus = PaymentStatus.PAID,
                OrderId = orderId,
                OrderCode = "ORD-1",
                OrderStatus = OrderStatus.DEPOSIT_PENDING,
                OrderFinalTotal = 100_000_000m,
                OrderPaidAmount = 0m,
                OrderRemainingAmount = 100_000_000m,
                TotalProjectCashCollected = 2_000_000m
            }
        };
        var service = new AdminProjectReportService(repository, financial);

        var result = await service.GetDetailAsync(projectId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("WAITING_DESIGNER", result.Data!.Header.PrimaryAttention!.Reason);
        Assert.Equal("DESIGNER_ASSIGNMENT", result.Data.Header.Stage);
        Assert.NotNull(result.Data.CurrentStageHealth);
        Assert.Equal(6, result.Data.FlowProgress.Stages.Count);
        Assert.Equal(2_000_000m, result.Data.CommercialSnapshot.ProjectStartFeeAmount);
        Assert.Equal("ORD-1", result.Data.CommercialSnapshot.OrderCode);
        Assert.Null(result.Data.TerminalSummary);
    }

    private sealed class FakeAdminProjectReportRepository : IAdminProjectReportRepository
    {
        public IReadOnlyList<AdminProjectReportCandidateReadModel> Candidates { get; init; } = [];
        public AdminProjectReportCandidateReadModel? Detail { get; init; }

        public Task<IReadOnlyList<AdminProjectReportCandidateReadModel>> GetCandidatesAsync(
            AdminProjectReportListQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Candidates);
        }

        public Task<AdminProjectReportCandidateReadModel?> GetCandidateAsync(
            Guid projectId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            if (Detail is not null && Detail.ProjectId == projectId)
            {
                return Task.FromResult<AdminProjectReportCandidateReadModel?>(Detail);
            }

            return Task.FromResult(Candidates.FirstOrDefault(c => c.ProjectId == projectId));
        }
    }

    private sealed class FakeFinancialReadRepository : IFinancialReadRepository
    {
        public AdminFinancialProjectRowReadModel? ProjectRow { get; init; }

        public Task<AdminFinancialSummaryReadModel> GetAdminSummaryAsync(
            DateTime fromUtc,
            DateTime toUtcExclusive,
            DateTime utcNow,
            string currency,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminFinancialSummaryReadModel());

        public Task<AdminFinancialReceivablesSummaryReadModel> GetReceivablesSummaryAsync(
            AdminFinancialReceivablesQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminFinancialReceivablesSummaryReadModel());

        public Task<IReadOnlyList<AdminFinancialReceivableItemReadModel>> GetReceivableItemsAsync(
            AdminFinancialReceivablesQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialReceivableItemReadModel>>([]);

        public Task<int> CountReceivableItemsAsync(
            AdminFinancialReceivablesQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>> GetPaymentBreakdownAsync(
            DateTime fromUtc,
            DateTime toUtcExclusive,
            DateTime utcNow,
            string currency,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>>([]);

        public Task<IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>> GetCollectedAmountsByPaymentTypeAsync(
            DateTime fromUtc,
            DateTime toUtcExclusive,
            string currency,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>>([]);

        public Task<IReadOnlyList<AdminFinancialProjectRowReadModel>> GetProjectFinancialRowsAsync(
            AdminFinancialProjectsQueryReadModel query,
            DateTime utcNow,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialProjectRowReadModel>>([]);

        public Task<int> CountProjectFinancialRowsAsync(
            AdminFinancialProjectsQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<AdminFinancialProjectRowReadModel?> GetProjectFinancialRowAsync(
            Guid projectId,
            DateTime utcNow,
            IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProjectRow);

        public Task<IReadOnlyList<AdminFinancialPaymentRowReadModel>> GetOperationalPaymentsAsync(
            AdminFinancialPaymentsQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialPaymentRowReadModel>>([]);

        public Task<int> CountOperationalPaymentsAsync(
            AdminFinancialPaymentsQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<AdminFinancialExceptionRowReadModel>> GetFinancialExceptionsAsync(
            AdminFinancialExceptionsQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminFinancialExceptionRowReadModel>>([]);

        public Task<int> CountFinancialExceptionsAsync(
            AdminFinancialExceptionsQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
