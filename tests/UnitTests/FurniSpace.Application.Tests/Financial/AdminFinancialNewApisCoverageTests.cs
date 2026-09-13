#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Services.Financial;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Financial;

public sealed class AdminFinancialNewApisCoverageTests
{
    [Fact]
    public async Task GetReceivableOrderDetailAsync_MapsFullDetail()
    {
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var repo = new FakeRepo
        {
            ReceivableDetail = new AdminFinancialReceivableDetailReadModel
            {
                OrderId = orderId,
                OrderCode = "ORD-1",
                OrderStatus = OrderStatus.DELIVERED,
                ConfirmedAt = new DateTime(2026, 8, 1, 3, 0, 0, DateTimeKind.Utc),
                FinalTotalAmount = 21600m,
                PaidAmount = 10000m,
                RemainingAmount = 11600m,
                ProjectId = Guid.NewGuid(),
                ProjectCode = "PRJ-1",
                ProjectName = "Project",
                CustomerId = Guid.NewGuid(),
                CustomerName = "Customer",
                CollectionState = AdminFinancialCollectionStates.NotCreated,
                ReceivableAgeDays = 10,
                PaymentProgressPercentage = 46.3m,
                LastPaidAt = new DateTime(2026, 8, 10, 3, 0, 0, DateTimeKind.Utc),
                ActivePaymentId = paymentId,
                ActivePaymentCode = "PAY-1",
                ActivePaymentType = PaymentType.REMAINING_PAYMENT,
                ActivePaymentAmount = 11600m,
                ActivePaymentStatus = PaymentStatus.PENDING,
                ActivePaymentExpiredAt = new DateTime(2026, 8, 20, 3, 0, 0, DateTimeKind.Utc),
                PaymentRounds =
                [
                    new AdminFinancialReceivablePaymentRoundReadModel
                    {
                        PaymentId = Guid.NewGuid(),
                        PaymentCode = "PAY-DEP",
                        PaymentType = PaymentType.DEPOSIT,
                        Amount = 10000m,
                        Status = "PAID",
                        Provider = PaymentProvider.PAYOS,
                        CreatedAt = new DateTime(2026, 8, 2, 3, 0, 0, DateTimeKind.Utc),
                        PaidAt = new DateTime(2026, 8, 2, 4, 0, 0, DateTimeKind.Utc),
                        AttemptCount = 1,
                        FailedAttemptCount = 0
                    },
                    new AdminFinancialReceivablePaymentRoundReadModel
                    {
                        PaymentType = PaymentType.REMAINING_PAYMENT,
                        Amount = 11600m,
                        Status = AdminFinancialCollectionStates.NotCreated
                    }
                ]
            }
        };

        var result = await new AdminFinancialService(repo).GetReceivableOrderDetailAsync(orderId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(21600m, result.Data!.Summary.FinalTotalAmount);
        Assert.Equal(46.3m, result.Data.Summary.PaymentProgressPercentage);
        Assert.Equal(AdminFinancialCollectionStates.NotCreated, result.Data.Summary.CollectionState);
        Assert.Equal("Create remaining payment request", result.Data.SuggestedAction);
        Assert.Equal(2, result.Data.PaymentRounds.Count);
        Assert.NotNull(result.Data.ActivePayment);
        Assert.Equal(paymentId, result.Data.ActivePayment!.PaymentId);
        Assert.Equal(TimeSpan.FromHours(7), result.Data.Summary.LastPaidAt!.Value.Offset);
    }

    [Fact]
    public async Task GetProjectStatementAsync_MapsSummaryAndItems()
    {
        var projectId = Guid.NewGuid();
        var repo = new FakeRepo
        {
            Statement = new AdminFinancialProjectStatementReadModel
            {
                ProjectId = projectId,
                ProjectCode = "PRJ-S",
                ProjectName = "Statement",
                CustomerName = "Cust",
                OpeningBalance = 50m,
                TotalCollected = 200m,
                TotalRefunded = 20m,
                NetCollected = 180m,
                ClosingBalance = 230m,
                TotalItems = 2,
                Page = 1,
                PageSize = 10,
                Items =
                [
                    new AdminFinancialProjectStatementItemReadModel
                    {
                        EntryId = Guid.NewGuid(),
                        OccurredAt = new DateTime(2026, 8, 2, 3, 0, 0, DateTimeKind.Utc),
                        Direction = AdminFinancialStatementDirections.Credit,
                        EntryType = AdminFinancialStatementEntryTypes.Collection,
                        PaymentType = nameof(PaymentType.DEPOSIT),
                        Description = "Deposit collected",
                        ReferenceCode = "PAY-1",
                        Amount = 200m,
                        RunningBalance = 250m
                    }
                ]
            }
        };

        var result = await new AdminFinancialService(repo).GetProjectStatementAsync(
            projectId,
            new AdminFinancialProjectStatementQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                EntryType = "collection",
                Page = 1,
                PageSize = 10,
                SortDirection = "asc"
            });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(50m, result.Data!.Summary.OpeningBalance);
        Assert.Equal(230m, result.Data.Summary.ClosingBalance);
        Assert.Single(result.Data.Items);
        Assert.Equal(AdminFinancialStatementEntryTypes.Collection, result.Data.Items[0].EntryType);
        Assert.Equal("Cust", result.Data.Project.CustomerName);
    }

    [Fact]
    public async Task GetProjectStatementAsync_InvalidEntryType_ReturnsBadRequest()
    {
        var result = await new AdminFinancialService(new FakeRepo()).GetProjectStatementAsync(
            Guid.NewGuid(),
            new AdminFinancialProjectStatementQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                EntryType = "UNKNOWN"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.FilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetProjectStatementAsync_InvalidPage_ReturnsBadRequest()
    {
        var result = await new AdminFinancialService(new FakeRepo()).GetProjectStatementAsync(
            Guid.NewGuid(),
            new AdminFinancialProjectStatementQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                Page = 0,
                PageSize = 10
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.FilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetProjectStatementAsync_MissingProject_ReturnsNotFound()
    {
        var result = await new AdminFinancialService(new FakeRepo()).GetProjectStatementAsync(
            Guid.NewGuid(),
            new AdminFinancialProjectStatementQueryDto
            {
                From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7))
            });

        Assert.Equal(404, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.FinancialProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetProjectStatementAsync_InvalidDateRange_ReturnsBadRequest()
    {
        var result = await new AdminFinancialService(new FakeRepo()).GetProjectStatementAsync(
            Guid.NewGuid(),
            new AdminFinancialProjectStatementQueryDto
            {
                From = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                To = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7))
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.DateRangeInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetReceivableOrderDetailAsync_MissingOrder_ReturnsNotFound()
    {
        var result = await new AdminFinancialService(new FakeRepo()).GetReceivableOrderDetailAsync(Guid.NewGuid());
        Assert.Equal(404, result.Status);
        Assert.Equal(AdminFinancialErrorCodes.OrderNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(AdminFinancialCollectionStates.Pending, "Follow up pending payment with customer")]
    [InlineData(AdminFinancialCollectionStates.Processing, "Wait for provider confirmation")]
    [InlineData(AdminFinancialCollectionStates.Expired, "Recreate expired payment request")]
    [InlineData(AdminFinancialCollectionStates.Failed, "Retry failed payment collection")]
    public async Task GetReceivableOrderDetailAsync_SuggestedAction_ByState(string state, string expected)
    {
        var repo = new FakeRepo
        {
            ReceivableDetail = new AdminFinancialReceivableDetailReadModel
            {
                OrderId = Guid.NewGuid(),
                OrderCode = "ORD",
                FinalTotalAmount = 1,
                ProjectId = Guid.NewGuid(),
                ProjectName = "P",
                CustomerId = Guid.NewGuid(),
                CollectionState = state,
                PaymentRounds = []
            }
        };

        var result = await new AdminFinancialService(repo).GetReceivableOrderDetailAsync(repo.ReceivableDetail.OrderId);
        Assert.Equal(200, result.Status);
        Assert.Equal(expected, result.Data!.SuggestedAction);
    }

    [Fact]
    public async Task GetReceivablesAsync_PassesConfirmedFromAliasAndNewFilters()
    {
        var repo = new FakeRepo
        {
            ReceivablesSummary = new AdminFinancialReceivablesSummaryReadModel
            {
                WithoutPaymentCount = 1,
                ActiveCollectionCount = 2,
                ExpiredPaymentCount = 3,
                FailedPaymentCount = 4,
                ContractedReceivableAmount = 100m,
                OrdersWithReceivableCount = 10
            },
            ReceivableItems =
            [
                new AdminFinancialReceivableItemReadModel
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectCode = "PRJ",
                    ProjectName = "P",
                    CustomerId = Guid.NewGuid(),
                    CustomerName = "C",
                    OrderId = Guid.NewGuid(),
                    OrderCode = "ORD",
                    OrderStatus = OrderStatus.DELIVERED,
                    ConfirmedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    FinalTotalAmount = 100m,
                    PaidAmount = 40m,
                    RemainingAmount = 60m,
                    PaymentProgressPercentage = 40m,
                    CollectionState = AdminFinancialCollectionStates.Pending,
                    ReceivableAgeDays = 5,
                    LastPaidAt = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                    ActivePaymentId = Guid.NewGuid(),
                    ActivePaymentType = PaymentType.DEPOSIT,
                    ActivePaymentAmount = 60m,
                    ActivePaymentStatus = PaymentStatus.PENDING,
                    ActivePaymentExpiredAt = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                    LastPaymentFailureReason = "Declined"
                }
            ],
            ReceivableTotalItems = 1
        };

        var result = await new AdminFinancialService(repo).GetReceivablesAsync(
            new AdminFinancialReceivablesQueryDto
            {
                Keyword = "ORD",
                CollectionState = "pending",
                MinAgeDays = 1,
                MaxAgeDays = 10,
                ConfirmedFrom = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
                ConfirmedTo = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7)),
                SortBy = "receivableAgeDays",
                SortDirection = "asc"
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(1, result.Data!.WithoutPaymentCount);
        Assert.Equal(2, result.Data.ActiveCollectionCount);
        Assert.Equal(3, result.Data.ExpiredPaymentCount);
        Assert.Equal(4, result.Data.FailedPaymentCount);
        Assert.Equal(AdminFinancialCollectionStates.Pending, result.Data.Items[0].CollectionState);
        Assert.Equal("Declined", result.Data.Items[0].LastPaymentFailureReason);
        Assert.Equal("ORD", repo.ReceivablesQuery!.Keyword);
        Assert.Equal(AdminFinancialCollectionStates.Pending, repo.ReceivablesQuery.CollectionState);
        Assert.Equal(1, repo.ReceivablesQuery.MinAgeDays);
        Assert.Equal(10, repo.ReceivablesQuery.MaxAgeDays);
    }

    [Fact]
    public void FinancialDtos_PropertyCoverage_TouchesNewModels()
    {
        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var statementQuery = new AdminFinancialProjectStatementQueryDto
        {
            From = now,
            To = now,
            EntryType = "COLLECTION",
            PaymentType = PaymentType.DEPOSIT,
            Status = "PAID",
            Provider = PaymentProvider.PAYOS,
            Page = 1,
            PageSize = 10,
            SortDirection = "desc"
        };
        _ = statementQuery.From;
        _ = statementQuery.To;
        _ = statementQuery.EntryType;
        _ = statementQuery.PaymentType;
        _ = statementQuery.Status;
        _ = statementQuery.Provider;
        _ = statementQuery.Page;
        _ = statementQuery.PageSize;
        _ = statementQuery.SortDirection;

        var statementItem = new AdminFinancialProjectStatementItemDto
        {
            EntryId = entryId,
            OccurredAt = now,
            Direction = AdminFinancialStatementDirections.Credit,
            EntryType = AdminFinancialStatementEntryTypes.Collection,
            PaymentType = "DEPOSIT",
            Description = "d",
            ReferenceCode = "r",
            OrderId = orderId,
            OrderCode = "o",
            PaymentId = paymentId,
            Provider = "PAYOS",
            Status = "PAID",
            Amount = 10,
            RunningBalance = 10
        };
        _ = statementItem.EntryId;
        _ = statementItem.OccurredAt;
        _ = statementItem.Direction;
        _ = statementItem.EntryType;
        _ = statementItem.PaymentType;
        _ = statementItem.Description;
        _ = statementItem.ReferenceCode;
        _ = statementItem.OrderId;
        _ = statementItem.OrderCode;
        _ = statementItem.PaymentId;
        _ = statementItem.Provider;
        _ = statementItem.Status;
        _ = statementItem.Amount;
        _ = statementItem.RunningBalance;

        var statement = new AdminFinancialProjectStatementDto
        {
            Project = new AdminFinancialProjectStatementProjectDto
            {
                ProjectId = projectId,
                ProjectCode = "P",
                ProjectName = "N",
                CustomerName = "C"
            },
            Summary = new AdminFinancialProjectStatementSummaryDto
            {
                OpeningBalance = 1,
                TotalCollected = 2,
                TotalRefunded = 3,
                NetCollected = -1,
                ClosingBalance = 0
            },
            Items = [statementItem],
            Page = 1,
            PageSize = 10,
            TotalItems = 1,
            TotalPages = 1
        };
        _ = statement.Project.ProjectId;
        _ = statement.Project.ProjectCode;
        _ = statement.Project.ProjectName;
        _ = statement.Project.CustomerName;
        _ = statement.Summary.OpeningBalance;
        _ = statement.Summary.TotalCollected;
        _ = statement.Summary.TotalRefunded;
        _ = statement.Summary.NetCollected;
        _ = statement.Summary.ClosingBalance;
        _ = statement.Items;
        _ = statement.Page;
        _ = statement.PageSize;
        _ = statement.TotalItems;
        _ = statement.TotalPages;

        var round = new AdminFinancialReceivablePaymentRoundDto
        {
            PaymentId = paymentId,
            PaymentCode = "PAY",
            PaymentType = PaymentType.DEPOSIT,
            Amount = 1,
            Status = "PAID",
            Provider = PaymentProvider.PAYOS,
            CreatedAt = now,
            PaidAt = now,
            ExpiredAt = now,
            AttemptCount = 1,
            FailedAttemptCount = 0,
            LastFailureReason = "x"
        };
        _ = round.PaymentId;
        _ = round.PaymentCode;
        _ = round.PaymentType;
        _ = round.Amount;
        _ = round.Status;
        _ = round.Provider;
        _ = round.CreatedAt;
        _ = round.PaidAt;
        _ = round.ExpiredAt;
        _ = round.AttemptCount;
        _ = round.FailedAttemptCount;
        _ = round.LastFailureReason;

        var detail = new AdminFinancialReceivableDetailDto
        {
            Order = new AdminFinancialReceivableOrderInfoDto
            {
                OrderId = orderId,
                OrderCode = "O",
                OrderStatus = OrderStatus.DELIVERED,
                ConfirmedAt = now,
                FinalTotalAmount = 1
            },
            Project = new AdminFinancialReceivableProjectInfoDto
            {
                ProjectId = projectId,
                ProjectCode = "P",
                ProjectName = "N"
            },
            Customer = new AdminFinancialReceivableCustomerInfoDto
            {
                CustomerId = customerId,
                CustomerName = "C"
            },
            Summary = new AdminFinancialReceivableDetailSummaryDto
            {
                FinalTotalAmount = 1,
                PaidAmount = 1,
                RemainingAmount = 0,
                PaymentProgressPercentage = 100,
                ReceivableAgeDays = 1,
                CollectionState = "PENDING",
                LastPaidAt = now
            },
            PaymentRounds = [round],
            ActivePayment = new AdminFinancialReceivableActivePaymentDto
            {
                PaymentId = paymentId,
                PaymentCode = "PAY",
                PaymentType = PaymentType.DEPOSIT,
                Amount = 1,
                Status = PaymentStatus.PENDING,
                ExpiredAt = now
            },
            SuggestedAction = "x"
        };
        _ = detail.Order.OrderId;
        _ = detail.Order.OrderCode;
        _ = detail.Order.OrderStatus;
        _ = detail.Order.ConfirmedAt;
        _ = detail.Order.FinalTotalAmount;
        _ = detail.Project.ProjectId;
        _ = detail.Project.ProjectCode;
        _ = detail.Project.ProjectName;
        _ = detail.Customer.CustomerId;
        _ = detail.Customer.CustomerName;
        _ = detail.Summary.FinalTotalAmount;
        _ = detail.Summary.PaidAmount;
        _ = detail.Summary.RemainingAmount;
        _ = detail.Summary.PaymentProgressPercentage;
        _ = detail.Summary.ReceivableAgeDays;
        _ = detail.Summary.CollectionState;
        _ = detail.Summary.LastPaidAt;
        _ = detail.PaymentRounds;
        _ = detail.ActivePayment!.PaymentId;
        _ = detail.ActivePayment.PaymentCode;
        _ = detail.ActivePayment.PaymentType;
        _ = detail.ActivePayment.Amount;
        _ = detail.ActivePayment.Status;
        _ = detail.ActivePayment.ExpiredAt;
        _ = detail.SuggestedAction;

        Assert.Equal(AdminFinancialStatementDirections.Debit, "DEBIT");
        Assert.Equal(AdminFinancialStatementEntryTypes.Adjustment, "ADJUSTMENT");
        Assert.Equal(AdminFinancialStatementEntryTypes.Refund, "REFUND");
        Assert.Equal(AdminFinancialCollectionStates.Failed, "FAILED");
        Assert.Equal(AdminFinancialErrorCodes.PaymentNotFound, "FINANCIAL_PAYMENT_NOT_FOUND");
        Assert.Equal(AdminFinancialErrorCodes.FilterInvalid, "FINANCIAL_FILTER_INVALID");
    }

    private sealed class FakeRepo : IFinancialReadRepository
    {
        public AdminFinancialReceivableDetailReadModel? ReceivableDetail { get; init; }
        public AdminFinancialProjectStatementReadModel? Statement { get; init; }
        public AdminFinancialReceivablesSummaryReadModel ReceivablesSummary { get; init; } = new();
        public IReadOnlyList<AdminFinancialReceivableItemReadModel> ReceivableItems { get; init; } = [];
        public int ReceivableTotalItems { get; init; }
        public AdminFinancialReceivablesQueryReadModel? ReceivablesQuery { get; private set; }

        public Task<AdminFinancialSummaryReadModel> GetAdminSummaryAsync(
            DateTime fromUtc, DateTime toUtcExclusive, DateTime utcNow, string currency,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminFinancialSummaryReadModel());

        public Task<AdminFinancialReceivablesSummaryReadModel> GetReceivablesSummaryAsync(
            AdminFinancialReceivablesQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ReceivablesQuery = query;
            return Task.FromResult(ReceivablesSummary);
        }

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialReceivableItemReadModel>> GetReceivableItemsAsync(
            AdminFinancialReceivablesQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ReceivablesQuery = query;
            return Task.FromResult(ReceivableItems);
        }

        public Task<int> CountReceivableItemsAsync(
            AdminFinancialReceivablesQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            ReceivablesQuery = query;
            return Task.FromResult(ReceivableTotalItems);
        }

        public Task<AdminFinancialReceivableDetailReadModel?> GetReceivableOrderDetailAsync(
            Guid orderId, DateTime utcNow, CancellationToken cancellationToken = default) =>
            Task.FromResult(ReceivableDetail);

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>> GetPaymentBreakdownAsync(
            DateTime fromUtc, DateTime toUtcExclusive, DateTime utcNow, string currency,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentTypeBreakdownReadModel>>([]);

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>> GetCollectedAmountsByPaymentTypeAsync(
            DateTime fromUtc, DateTime toUtcExclusive, string currency,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentTypeAmountReadModel>>([]);

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialProjectRowReadModel>> GetProjectFinancialRowsAsync(
            AdminFinancialProjectsQueryReadModel query, DateTime utcNow,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialProjectRowReadModel>>([]);

        public Task<int> CountProjectFinancialRowsAsync(
            AdminFinancialProjectsQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<AdminFinancialProjectRowReadModel?> GetProjectFinancialRowAsync(
            Guid projectId, DateTime utcNow,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminFinancialProjectRowReadModel?>(null);

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentRowReadModel>> GetOperationalPaymentsAsync(
            AdminFinancialPaymentsQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialPaymentRowReadModel>>([]);

        public Task<int> CountOperationalPaymentsAsync(
            AdminFinancialPaymentsQueryReadModel query,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<System.Collections.Generic.IReadOnlyList<AdminFinancialExceptionRowReadModel>> GetFinancialExceptionsAsync(
            AdminFinancialExceptionsQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<AdminFinancialExceptionRowReadModel>>([]);

        public Task<int> CountFinancialExceptionsAsync(
            AdminFinancialExceptionsQueryReadModel query, DateTime utcNow,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<AdminFinancialSummaryDrilldownReadModel> GetSummaryDrilldownAsync(
            AdminFinancialSummaryDrilldownQueryReadModel query,
            DateTime fromUtc, DateTime toUtcExclusive, DateTime utcNow, string currency,
            System.Collections.Generic.IReadOnlyCollection<PaymentType> canonicalPaymentTypes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminFinancialSummaryDrilldownReadModel { Metric = query.Metric });

        public Task<AdminFinancialProjectStatementReadModel?> GetProjectStatementAsync(
            AdminFinancialProjectStatementQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Statement);
    }
}
