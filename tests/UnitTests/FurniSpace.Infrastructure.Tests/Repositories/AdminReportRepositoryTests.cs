#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class AdminReportRepositoryTests
{
    [Fact]
    public async Task CountAccounts_ByStatusAndRole_ReturnsFacets()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var byStatus = await repository.CountAccountsByStatusAsync();
        var byRole = await repository.CountAccountsByRoleAsync();

        Assert.Contains(byStatus, row => row.Key == "ACTIVE" && row.Count >= 1);
        Assert.Contains(byRole, row => row.Key is "SALES" or "DESIGNER" or "PRODUCTION" or "CUSTOMER");
    }

    [Fact]
    public async Task GetProjectReportAsync_ComputesBucketsAgingAndRangeCounters()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var report = await repository.GetProjectReportAsync(seed.From, seed.To);

        Assert.True(report.ByStatus.Count > 0);
        Assert.True(report.ByBucket.Intake + report.ByBucket.Commercial + report.ByBucket.DesignMonitor
            + report.ByBucket.Fulfillment + report.ByBucket.Terminal + report.ByBucket.Other > 0);
        Assert.True(report.UnassignedIntakeCount >= 1);
        Assert.True(report.WaitingForDesignerCount >= 1);
        Assert.True(report.CompletedInRange >= 1);
        Assert.True(report.RejectedInRange >= 1);
        Assert.True(report.TotalNonTerminal >= 1);
        Assert.True(report.Aging.Over7Days >= 1);
    }

    [Fact]
    public async Task GetCommercialReportAsync_AggregatesQuotationsOrdersPayments()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var report = await repository.GetCommercialReportAsync(seed.From, seed.To);

        Assert.True(report.Quotations.SentInRange >= 1);
        Assert.True(report.Quotations.AcceptedInRange >= 1);
        Assert.True(report.Orders.CreatedInRange >= 1);
        Assert.True(report.Orders.GmvInRange > 0);
        Assert.True(report.Payments.PaidAmountInRange > 0);
        Assert.True(report.Payments.ByType.Count > 0);
        Assert.True(report.Conversion.DepositsPaidInRange >= 1);
    }

    [Fact]
    public async Task GetProductionReportAsync_IncludesTopAssigneesAndOverdue()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var report = await repository.GetProductionReportAsync(seed.From, seed.To);

        Assert.True(report.OpenRequestCount >= 1);
        Assert.Equal(0, report.BlockedCount);
        Assert.True(report.OverdueCount >= 1);
        Assert.True(report.TopAssignees.Count >= 1);
        Assert.True(report.ItemsByStatus.Count >= 1);
    }

    [Fact]
    public async Task GetDeliveryReportAsync_ComputesProjectOrderAndScheduleMetrics()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var report = await repository.GetDeliveryReportAsync(seed.From, seed.To);

        Assert.True(report.Projects.ReadyForDelivery >= 1);
        Assert.True(report.Projects.Delivering >= 1);
        Assert.True(report.OrderItems.PartialDeliveryCount >= 1);
        Assert.True(report.Schedules.UpcomingDeliveryOrHandover + report.Schedules.OverdueDeliveryOrHandover >= 1);
    }

    [Fact]
    public async Task GetCatalogReportAsync_ComputesMissingVersionAnd3D()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var report = await repository.GetCatalogReportAsync();

        Assert.True(report.ProductsByStatus.Count >= 1);
        Assert.True(report.ProductsMissingActiveVersion >= 1);
        Assert.True(report.ProductsMissing3D >= 1);
        Assert.True(report.ProductsByCategory.Count >= 1);
        Assert.True(report.ProductsByBusinessType.Count >= 1);
        Assert.Contains(report.BusinessTypesByStatus, item => item.Key == "ACTIVE");
    }

    [Fact]
    public async Task GetProjectAgingAsync_FiltersReasonsBucketsAndSorts()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var (items, total) = await repository.GetProjectAgingAsync(
            thresholdDays: 7,
            bucket: null,
            reason: "UNASSIGNED_INTAKE",
            page: 1,
            pageSize: 20,
            sortBy: "AgeDaysDesc");

        Assert.True(total >= 1);
        Assert.All(items, item => Assert.Equal("UNASSIGNED_INTAKE", item.Reason));

        var (sorted, sortedTotal) = await repository.GetProjectAgingAsync(
            thresholdDays: 1,
            bucket: null,
            reason: null,
            page: 1,
            pageSize: 50,
            sortBy: "SubmittedAtAsc");
        Assert.True(sortedTotal >= 1);
        Assert.True(sorted.Count >= 1);
    }

    [Fact]
    public async Task GetCommercialTrendAsync_BuildsDayAndWeekPoints()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var day = await repository.GetCommercialTrendAsync(seed.From, seed.To, "day");
        Assert.Equal("day", day.Granularity);
        Assert.True(day.Points.Count >= 1);
        Assert.True(day.Totals.OrdersCreated >= 1);

        var week = await repository.GetCommercialTrendAsync(seed.From, seed.To.AddDays(7), "week");
        Assert.Equal("week", week.Granularity);
        Assert.True(week.Points.Count >= 1);
    }

    [Fact]
    public async Task GetCatalogBestsellersAsync_OrdersByQuantityAndRevenue()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var byQty = await repository.GetCatalogBestsellersAsync(seed.From, seed.To, "quantity", 10);
        Assert.Equal("quantity", byQty.Metric);
        Assert.True(byQty.Items.Count >= 1);
        Assert.NotNull(byQty.Items[0].ProductId);

        var byRevenue = await repository.GetCatalogBestsellersAsync(seed.From, seed.To, "revenue", 10);
        Assert.Equal("revenue", byRevenue.Metric);
        Assert.True(byRevenue.Items.Count >= 1);
    }

    [Fact]
    public async Task GetDeliveryReviewsAsync_ReturnsSummaryAndPagedItems()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var report = await repository.GetDeliveryReviewsAsync(seed.From, seed.To, page: 1, pageSize: 20);

        Assert.True(report.Summary.ReviewCount >= 1);
        Assert.NotNull(report.Summary.AverageOverallRating);
        Assert.True(report.Items.Count >= 1);
        Assert.Equal(1, report.Page);
    }

    [Fact]
    public async Task GetProductionWorkloadAsync_ComputesCapacityFiltersAndSummary()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new AdminReportRepository(context);

        var (items, total, summary) = await repository.GetProductionWorkloadAsync(
            page: 1,
            pageSize: 20,
            maxActiveRequests: 5,
            search: "Prod",
            capacityState: null,
            sortBy: "OpenRequestCountDesc");

        Assert.True(total >= 1);
        Assert.True(items.Count >= 1);
        Assert.True(summary.TotalActiveStaff >= 1);
        Assert.Equal(5, summary.MaxActiveRequests);

        var (filtered, _, _) = await repository.GetProductionWorkloadAsync(
            page: 1,
            pageSize: 20,
            maxActiveRequests: 5,
            search: null,
            capacityState: "AVAILABLE",
            sortBy: "AvailableSlotDesc");
        Assert.All(filtered, item => Assert.Equal("AVAILABLE", item.CapacityState));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Seeded> SeedAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        var from = now.AddDays(-14);
        var to = now.AddDays(1);

        var customerRole = Role("CUSTOMER");
        var salesRole = Role("SALES");
        var designerRole = Role("DESIGNER");
        var productionRole = Role("PRODUCTION");

        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var prodStaffId = Guid.NewGuid();
        var prodFullId = Guid.NewGuid();

        context.RoleSet.AddRange(customerRole, salesRole, designerRole, productionRole);
        context.AccountSet.AddRange(
            Account(customerId, customerRole.RoleId, "c@example.com", "Customer"),
            Account(salesId, salesRole.RoleId, "s@example.com", "Sales"),
            Account(designerId, designerRole.RoleId, "d@example.com", "Designer"),
            Account(prodStaffId, productionRole.RoleId, "prod.a@example.com", "Prod A"),
            Account(prodFullId, productionRole.RoleId, "prod.b@example.com", "Prod B"),
            Account(Guid.NewGuid(), salesRole.RoleId, "deleted@example.com", "Deleted", deletedAt: now));

        var unassignedProjectId = Guid.NewGuid();
        var waitingDesignerProjectId = Guid.NewGuid();
        var commercialProjectId = Guid.NewGuid();
        var completedProjectId = Guid.NewGuid();
        var rejectedProjectId = Guid.NewGuid();
        var readyDeliveryProjectId = Guid.NewGuid();
        var deliveringProjectId = Guid.NewGuid();
        var deliveredProjectId = Guid.NewGuid();

        context.ProjectSet.AddRange(
            Project(unassignedProjectId, customerId, null, null, ProjectStatus.SUBMITTED, now.AddDays(-20)),
            Project(waitingDesignerProjectId, customerId, salesId, null, ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, now.AddDays(-10)),
            Project(commercialProjectId, customerId, salesId, designerId, ProjectStatus.QUOTATION_SENT, now.AddDays(-8)),
            Project(completedProjectId, customerId, salesId, designerId, ProjectStatus.COMPLETED, now.AddDays(-5), completedAt: now.AddDays(-2)),
            Project(rejectedProjectId, customerId, salesId, null, ProjectStatus.REJECTED, now.AddDays(-6), rejectedAt: now.AddDays(-1)),
            Project(readyDeliveryProjectId, customerId, salesId, designerId, ProjectStatus.READY_FOR_DELIVERY, now.AddDays(-4)),
            Project(deliveringProjectId, customerId, salesId, designerId, ProjectStatus.DELIVERING, now.AddDays(-3)),
            Project(deliveredProjectId, customerId, salesId, designerId, ProjectStatus.DELIVERED, now.AddDays(-2), updatedAt: now.AddDays(-1)));

        var proposalId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        context.QuotationSet.AddRange(
            new Quotation
            {
                QuotationId = quotationId,
                ProjectId = commercialProjectId,
                ProposalId = proposalId,
                QuotationCode = "QT-1",
                Status = QuotationStatus.SENT,
                SubtotalAmount = 1000m,
                TotalDiscountAmount = 0m,
                PreVatAmount = 1000m,
                VatRate = 0.08m,
                VatAmount = 80m,
                TotalAmount = 1000m,
                DepositAmount = 300m,
                SentAt = now.AddDays(-3),
                AcceptedAt = now.AddDays(-2)
            },
            new Quotation
            {
                QuotationId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                ProposalId = Guid.NewGuid(),
                QuotationCode = "QT-2",
                Status = QuotationStatus.REVISION_REQUESTED,
                SubtotalAmount = 0m,
                TotalDiscountAmount = 0m,
                PreVatAmount = 0m,
                VatRate = 0.08m,
                VatAmount = 0m,
                TotalAmount = 0m,
                DepositAmount = 0m
            },
            new Quotation
            {
                QuotationId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                ProposalId = Guid.NewGuid(),
                QuotationCode = "QT-3",
                Status = QuotationStatus.REVISED,
                SubtotalAmount = 0m,
                TotalDiscountAmount = 0m,
                PreVatAmount = 0m,
                VatRate = 0.08m,
                VatAmount = 0m,
                TotalAmount = 0m,
                DepositAmount = 0m
            });

        var orderId = Guid.NewGuid();
        var deliveryOrderId = Guid.NewGuid();
        context.OrderSet.AddRange(
            new Order
            {
                OrderId = orderId,
                ProjectId = commercialProjectId,
                QuotationId = quotationId,
                OrderCode = "ORD-1",
                CustomerId = customerId,
                SalesId = salesId,
                OriginalTotalAmount = 1000m,
                FinalTotalAmount = 1000m,
                PaidAmount = 400m,
                RemainingAmount = 600m,
                Status = OrderStatus.DEPOSIT_PAID,
                CreatedAt = now.AddDays(-2)
            },
            new Order
            {
                OrderId = deliveryOrderId,
                ProjectId = deliveringProjectId,
                QuotationId = quotationId,
                OrderCode = "ORD-2",
                CustomerId = customerId,
                OriginalTotalAmount = 500m,
                FinalTotalAmount = 500m,
                Status = OrderStatus.DELIVERING,
                CreatedAt = now.AddDays(-1)
            });

        var productVersionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        context.CategorySet.Add(new Category
        {
            CategoryId = categoryId,
            CategoryName = "Chair",
            Status = ProductStatus.ACTIVE,
            CreatedAt = now
        });
        context.BusinessTypeSet.Add(new BusinessType
        {
            Id = 1,
            Code = "CAFE",
            Name = "Cafe",
            Status = true,
            CreatedAt = now
        });
        context.ProductSet.AddRange(
            new Product
            {
                ProductId = productId,
                CategoryId = categoryId,
                BusinessTypeIds = [1],
                ProductName = "Chair X",
                Status = ProductStatus.ACTIVE,
                CreatedAt = now
            },
            new Product
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Missing Version",
                Status = ProductStatus.ACTIVE,
                CreatedAt = now
            });
        context.ProductVersionSet.Add(new ProductVersion
        {
            ProductVersionId = productVersionId,
            ProductId = productId,
            VersionCode = "CHAIR-X-01",
            VersionName = "Default",
            Status = ProductStatus.ACTIVE,
            CreatedAt = now
        });
        context.FileLinkSet.Add(new FileLink
        {
            FileLinkId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            ReferenceType = "PRODUCT",
            ReferenceId = productId,
            FileType = FileType.MODEL_3D
        });

        var orderItemId = Guid.NewGuid();
        context.OrderItemSet.Add(new OrderItem
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            ProductVersionId = productVersionId,
            ProductNameSnapshot = "Chair X",
            ProductVersionCodeSnapshot = "CHAIR-X-01",
            Quantity = 5,
            DeliveredQuantity = 2,
            UnitPrice = 200m,
            DiscountAmount = 0m,
            SubtotalAmount = 1000m,
            CustomerConfirmedAt = now.AddDays(-1)
        });

        context.PaymentSet.AddRange(
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                OrderId = orderId,
                PaymentCode = "PAY-1",
                PaymentType = PaymentType.DEPOSIT,
                Amount = 400m,
                Status = PaymentStatus.PAID,
                PaidAt = now.AddDays(-2)
            },
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                PaymentCode = "PAY-2",
                PaymentType = PaymentType.OTHER,
                Amount = 50m,
                Status = PaymentStatus.EXPIRED
            },
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                PaymentCode = "PAY-3",
                Amount = 10m,
                Status = PaymentStatus.CANCELLED
            });

        var productionRequestId = Guid.NewGuid();
        context.ProductionRequestSet.AddRange(
            new ProductionRequest
            {
                ProductionRequestId = productionRequestId,
                ProjectId = commercialProjectId,
                OrderId = orderId,
                AssignedTo = prodStaffId,
                Status = ProductionRequestStatus.IN_PRODUCTION,
                EstimatedCompletionDate = DateOnly.FromDateTime(now.AddDays(-1)),
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-1)
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                OrderId = orderId,
                AssignedTo = prodStaffId,
                Status = ProductionRequestStatus.FEASIBLE,
                CreatedAt = now.AddDays(-2)
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                OrderId = orderId,
                AssignedTo = null,
                Status = ProductionRequestStatus.PENDING_REVIEW,
                CreatedAt = now.AddDays(-1)
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                OrderId = orderId,
                AssignedTo = prodFullId,
                Status = ProductionRequestStatus.FEASIBLE,
                CreatedAt = now.AddDays(-1)
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                OrderId = orderId,
                AssignedTo = prodFullId,
                Status = ProductionRequestStatus.IN_PRODUCTION,
                CreatedAt = now.AddDays(-1)
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                OrderId = orderId,
                AssignedTo = prodFullId,
                Status = ProductionRequestStatus.IN_PRODUCTION,
                CreatedAt = now.AddDays(-1)
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                OrderId = orderId,
                AssignedTo = prodFullId,
                Status = ProductionRequestStatus.IN_PRODUCTION,
                CreatedAt = now.AddDays(-1)
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                OrderId = orderId,
                AssignedTo = prodFullId,
                Status = ProductionRequestStatus.IN_PRODUCTION,
                CreatedAt = now.AddDays(-1)
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = commercialProjectId,
                OrderId = orderId,
                AssignedTo = prodStaffId,
                Status = ProductionRequestStatus.COMPLETED,
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-1)
            });

        context.ProductionItemSet.Add(new ProductionItem
        {
            ProductionItemId = Guid.NewGuid(),
            ProductionRequestId = productionRequestId,
            OrderItemId = orderItemId,
            ProductVersionId = productVersionId,
            Quantity = 1,
            Status = ProductionItemStatus.IN_PRODUCTION
        });

        context.ProjectScheduleSet.AddRange(
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = deliveringProjectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Title = "Delivery",
                ScheduledStart = now.AddDays(2),
                Status = ProjectScheduleStatus.CONFIRMED,
                CreatedAt = now
            },
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = deliveringProjectId,
                ScheduleType = ProjectScheduleType.HANDOVER,
                Title = "Handover",
                ScheduledStart = now.AddDays(-3),
                ScheduledEnd = now.AddDays(-1),
                Status = ProjectScheduleStatus.CONFIRMED,
                CreatedAt = now
            });

        context.ProjectReviewSet.Add(new ProjectReview
        {
            ReviewId = Guid.NewGuid(),
            ProjectId = deliveredProjectId,
            CustomerId = customerId,
            Rating = 5,
            DeliveryRating = 4,
            Comment = "Great",
            CreatedAt = now.AddDays(-1)
        });

        await context.SaveChangesAsync();
        return new Seeded(from, to);
    }

    private static Role Role(string name) => new()
    {
        RoleId = Guid.NewGuid(),
        RoleName = name,
        Description = name
    };

    private static Account Account(
        Guid id,
        Guid roleId,
        string email,
        string fullName,
        DateTime? deletedAt = null) => new()
    {
        AccountId = id,
        RoleId = roleId,
        Email = email,
        PasswordHash = "hash",
        FullName = fullName,
        Status = AccountStatus.ACTIVE,
        CreatedAt = DateTime.UtcNow,
        DeletedAt = deletedAt
    };

    private static Project Project(
        Guid id,
        Guid customerId,
        Guid? salesId,
        Guid? designerId,
        ProjectStatus status,
        DateTime submittedAt,
        DateTime? completedAt = null,
        DateTime? rejectedAt = null,
        DateTime? updatedAt = null) => new()
    {
        ProjectId = id,
        CustomerId = customerId,
        AssignedSalesId = salesId,
        AssignedDesignerId = designerId,
        ProjectCode = $"PRJ-{id:N}"[..12],
        ProjectName = $"Project {status}",
        FurnitureRequirement = "Tables",
        Status = status,
        SubmittedAt = submittedAt,
        CreatedAt = submittedAt,
        CompletedAt = completedAt,
        RejectedAt = rejectedAt,
        UpdatedAt = updatedAt
    };

    private sealed record Seeded(DateTime From, DateTime To);
}
