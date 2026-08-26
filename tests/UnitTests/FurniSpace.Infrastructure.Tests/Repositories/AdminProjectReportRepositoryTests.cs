#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Reports;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class AdminProjectReportRepositoryTests
{
    [Fact]
    public async Task GetCandidatesAsync_EmptyDatabase_ReturnsEmpty()
    {
        await using var context = CreateContext();
        var repository = new AdminProjectReportRepository(context);

        var rows = await repository.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel { ExcludeTerminal = true },
            DateTime.UtcNow);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetCandidateAsync_Missing_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new AdminProjectReportRepository(context);

        var row = await repository.GetCandidateAsync(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Null(row);
    }

    [Fact]
    public async Task GetCandidatesAsync_AppliesFilters_AndLoadsRelatedSignals()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new AdminProjectReportRepository(context);
        var now = DateTime.UtcNow;

        var byKeyword = await repository.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel
            {
                Keyword = "cafe district",
                ExcludeTerminal = true
            },
            now);
        Assert.Contains(byKeyword, r => r.ProjectId == seed.ActiveProjectId);

        var byStage = await repository.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel
            {
                StageStatuses =
                [
                    ProjectStatus.IN_PRODUCTION,
                    ProjectStatus.READY_FOR_DELIVERY
                ],
                ExcludeTerminal = true
            },
            now);
        Assert.Contains(byStage, r => r.ProjectId == seed.ProductionProjectId);

        var bySales = await repository.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel
            {
                SalesId = seed.SalesId,
                ExcludeTerminal = false
            },
            now);
        Assert.True(bySales.Count >= 1);

        var byDesigner = await repository.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel
            {
                DesignerId = seed.DesignerId,
                ExcludeTerminal = true
            },
            now);
        Assert.Contains(byDesigner, r => r.ProjectId == seed.MeasurementProjectId);

        var byStatus = await repository.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel
            {
                ProjectStatus = ProjectStatus.MEASUREMENT_REQUIRED,
                ExcludeTerminal = true
            },
            now);
        Assert.Single(byStatus);
        Assert.Equal(seed.MeasurementProjectId, byStatus[0].ProjectId);

        var byRange = await repository.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel
            {
                FromUtc = now.AddDays(-30),
                ToUtcExclusive = now.AddDays(1),
                ExcludeTerminal = false
            },
            now);
        Assert.True(byRange.Count >= 3);

        var excludeTerminal = await repository.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel { ExcludeTerminal = true },
            now);
        Assert.DoesNotContain(excludeTerminal, r => r.Status is ProjectStatus.COMPLETED or ProjectStatus.REJECTED);
    }

    [Fact]
    public async Task GetCandidateAsync_LoadsPaymentsOrdersProductionAndSchedules()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new AdminProjectReportRepository(context);
        var now = DateTime.UtcNow;

        var production = await repository.GetCandidateAsync(seed.ProductionProjectId, now);
        Assert.NotNull(production);
        Assert.Equal(PaymentStatus.PAID, production!.ProjectStartFeeStatus);
        Assert.True(production.CancelledProductionItemCount >= 1);
        Assert.NotNull(production.LatestOrderId);
        Assert.NotNull(production.LatestProductionRequestId);
        Assert.NotNull(production.LatestQuotationId);
        Assert.Equal("Sales Alpha", production.AssignedSalesName);
        Assert.Equal("Customer Alpha", production.CustomerName);

        var measurement = await repository.GetCandidateAsync(seed.MeasurementProjectId, now);
        Assert.NotNull(measurement);
        Assert.True(measurement!.HasOverdueMeasurementSchedule);
        Assert.Equal("Designer Beta", measurement.AssignedDesignerName);

        var delivery = await repository.GetCandidateAsync(seed.DeliveryProjectId, now);
        Assert.NotNull(delivery);
        Assert.True(delivery!.HasOverdueDeliverySchedule);
        Assert.True(delivery.HasExpiredCollectiblePayment);
        Assert.NotNull(delivery.ActivePaymentCreatedAt);
        Assert.True(delivery.QuotationRevisionRequestedCount >= 1);
    }

    [Fact]
    public async Task GetCandidatesAsync_KeywordMatchesProjectCodeOrCustomer()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new AdminProjectReportRepository(context);

        var byCode = await repository.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel
            {
                Keyword = seed.ActiveProjectCode[..6],
                ExcludeTerminal = false
            },
            DateTime.UtcNow);
        Assert.Contains(byCode, r => r.ProjectId == seed.ActiveProjectId);

        var byCustomer = await repository.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel
            {
                Keyword = "customer alpha",
                ExcludeTerminal = false
            },
            DateTime.UtcNow);
        Assert.True(byCustomer.Count >= 1);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeedData> SeedAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        var customerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER", Description = "c" };
        var salesRole = new Role { RoleId = Guid.NewGuid(), RoleName = "SALES", Description = "s" };
        var designerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "DESIGNER", Description = "d" };
        context.RoleSet.AddRange(customerRole, salesRole, designerRole);

        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        context.AccountSet.AddRange(
            new Account
            {
                AccountId = customerId,
                RoleId = customerRole.RoleId,
                Email = "customer@test.com",
                PasswordHash = "x",
                FullName = "Customer Alpha",
                Status = AccountStatus.ACTIVE,
                CreatedAt = now
            },
            new Account
            {
                AccountId = salesId,
                RoleId = salesRole.RoleId,
                Email = "sales@test.com",
                PasswordHash = "x",
                FullName = "Sales Alpha",
                Status = AccountStatus.ACTIVE,
                CreatedAt = now
            },
            new Account
            {
                AccountId = designerId,
                RoleId = designerRole.RoleId,
                Email = "designer@test.com",
                PasswordHash = "x",
                FullName = "Designer Beta",
                Status = AccountStatus.ACTIVE,
                CreatedAt = now
            });

        var activeProjectId = Guid.NewGuid();
        var activeCode = "PRJ-CAFE-001";
        var productionProjectId = Guid.NewGuid();
        var measurementProjectId = Guid.NewGuid();
        var deliveryProjectId = Guid.NewGuid();
        var completedProjectId = Guid.NewGuid();

        context.ProjectSet.AddRange(
            new Project
            {
                ProjectId = activeProjectId,
                CustomerId = customerId,
                AssignedSalesId = salesId,
                ProjectCode = activeCode,
                ProjectName = "Cafe District 1",
                BusinessType = "Cafe",
                ProjectAddress = "Q1",
                FurnitureRequirement = "Tables",
                Status = ProjectStatus.IN_CONSULTATION,
                SubmittedAt = now.AddDays(-5),
                SalesAssignedAt = now.AddDays(-4),
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-1)
            },
            new Project
            {
                ProjectId = productionProjectId,
                CustomerId = customerId,
                AssignedSalesId = salesId,
                AssignedDesignerId = designerId,
                ProjectCode = "PRJ-PROD-001",
                ProjectName = "Production Shop",
                FurnitureRequirement = "Chairs",
                Status = ProjectStatus.IN_PRODUCTION,
                SubmittedAt = now.AddDays(-20),
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-2)
            },
            new Project
            {
                ProjectId = measurementProjectId,
                CustomerId = customerId,
                AssignedSalesId = salesId,
                AssignedDesignerId = designerId,
                ProjectCode = "PRJ-MEAS-001",
                ProjectName = "Measurement Site",
                FurnitureRequirement = "Counters",
                Status = ProjectStatus.MEASUREMENT_REQUIRED,
                SubmittedAt = now.AddDays(-8),
                DesignerAssignedAt = now.AddDays(-3),
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now.AddDays(-1)
            },
            new Project
            {
                ProjectId = deliveryProjectId,
                CustomerId = customerId,
                AssignedSalesId = salesId,
                AssignedDesignerId = designerId,
                ProjectCode = "PRJ-DEL-001",
                ProjectName = "Delivery Site",
                FurnitureRequirement = "Sofas",
                Status = ProjectStatus.DELIVERING,
                SubmittedAt = now.AddDays(-25),
                CreatedAt = now.AddDays(-25),
                UpdatedAt = now.AddDays(-1)
            },
            new Project
            {
                ProjectId = completedProjectId,
                CustomerId = customerId,
                AssignedSalesId = salesId,
                ProjectCode = "PRJ-DONE-001",
                ProjectName = "Completed Site",
                FurnitureRequirement = "Done",
                Status = ProjectStatus.COMPLETED,
                SubmittedAt = now.AddDays(-40),
                CompletedAt = now.AddDays(-1),
                CreatedAt = now.AddDays(-40),
                UpdatedAt = now.AddDays(-1)
            });

        var quotationId = Guid.NewGuid();
        var revisionQuotationId = Guid.NewGuid();
        context.QuotationSet.AddRange(
            new Quotation
            {
                QuotationId = quotationId,
                ProjectId = productionProjectId,
                ProposalId = Guid.NewGuid(),
                QuotationCode = "QT-1",
                SubtotalAmount = 100,
                TotalDiscountAmount = 0,
                PreVatAmount = 100,
                VatRate = 0.1m,
                VatAmount = 10,
                TotalAmount = 110,
                DepositAmount = 33,
                Status = QuotationStatus.ACCEPTED,
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now.AddDays(-9)
            },
            new Quotation
            {
                QuotationId = revisionQuotationId,
                ProjectId = deliveryProjectId,
                ProposalId = Guid.NewGuid(),
                QuotationCode = "QT-2",
                SubtotalAmount = 100,
                TotalDiscountAmount = 0,
                PreVatAmount = 100,
                VatRate = 0.1m,
                VatAmount = 10,
                TotalAmount = 110,
                DepositAmount = 33,
                Status = QuotationStatus.REVISION_REQUESTED,
                CreatedAt = now.AddDays(-6),
                UpdatedAt = now.AddDays(-5)
            },
            new Quotation
            {
                QuotationId = Guid.NewGuid(),
                ProjectId = deliveryProjectId,
                ProposalId = Guid.NewGuid(),
                QuotationCode = "QT-3",
                SubtotalAmount = 100,
                TotalDiscountAmount = 0,
                PreVatAmount = 100,
                VatRate = 0.1m,
                VatAmount = 10,
                TotalAmount = 110,
                DepositAmount = 33,
                Status = QuotationStatus.REVISED,
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now.AddDays(-3)
            });

        var orderId = Guid.NewGuid();
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = productionProjectId,
            QuotationId = quotationId,
            OrderCode = "ORD-1",
            CustomerId = customerId,
            SalesId = salesId,
            VatRate = 0.1m,
            VatAmount = 0,
            OriginalTotalAmount = 100,
            FinalTotalAmount = 100,
            PaidAmount = 30,
            RemainingAmount = 70,
            Status = OrderStatus.IN_PRODUCTION,
            ConfirmedAt = now.AddDays(-8),
            CreatedAt = now.AddDays(-8)
        });

        context.PaymentSet.AddRange(
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = productionProjectId,
                PaymentCode = "PAY-SF",
                PaymentType = PaymentType.PROJECT_START_FEE,
                Amount = 2_000_000m,
                Status = PaymentStatus.PAID,
                PaidAt = now.AddDays(-15),
                CreatedAt = now.AddDays(-16)
            },
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = deliveryProjectId,
                PaymentCode = "PAY-EXP",
                PaymentType = PaymentType.DEPOSIT,
                Amount = 10,
                Status = PaymentStatus.EXPIRED,
                ExpiredAt = now.AddDays(-1),
                CreatedAt = now.AddDays(-3)
            },
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = deliveryProjectId,
                PaymentCode = "PAY-ACT",
                PaymentType = PaymentType.REMAINING_PAYMENT,
                Amount = 20,
                Status = PaymentStatus.PENDING,
                ExpiredAt = now.AddDays(3),
                CreatedAt = now.AddDays(-2)
            });

        var productionRequestId = Guid.NewGuid();
        context.ProductionRequestSet.Add(new ProductionRequest
        {
            ProductionRequestId = productionRequestId,
            ProjectId = productionProjectId,
            OrderId = orderId,
            Status = ProductionRequestStatus.IN_PRODUCTION,
            CreatedAt = now.AddDays(-5)
        });
        context.ProductionItemSet.Add(new ProductionItem
        {
            ProductionItemId = Guid.NewGuid(),
            ProductionRequestId = productionRequestId,
            OrderItemId = Guid.NewGuid(),
            Quantity = 1,
            Status = ProductionItemStatus.CANCELLED,
            CancellationReason = "Material unavailable"
        });

        context.ProjectScheduleSet.AddRange(
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = measurementProjectId,
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Measure",
                ScheduledStart = now.AddDays(-4),
                ScheduledEnd = now.AddDays(-1),
                Status = ProjectScheduleStatus.CONFIRMED,
                CreatedAt = now.AddDays(-4)
            },
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = deliveryProjectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Title = "Deliver",
                ScheduledStart = now.AddDays(-3),
                ScheduledEnd = now.AddDays(-1),
                Status = ProjectScheduleStatus.CONFIRMED,
                CreatedAt = now.AddDays(-3)
            },
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = deliveryProjectId,
                ScheduleType = ProjectScheduleType.HANDOVER,
                Title = "Handover",
                ScheduledStart = now.AddDays(-2),
                ScheduledEnd = now.AddDays(-1),
                Status = ProjectScheduleStatus.CONFIRMED,
                CreatedAt = now.AddDays(-2)
            });

        await context.SaveChangesAsync();
        return new SeedData(
            activeProjectId,
            activeCode,
            productionProjectId,
            measurementProjectId,
            deliveryProjectId,
            salesId,
            designerId);
    }

    private sealed record SeedData(
        Guid ActiveProjectId,
        string ActiveProjectCode,
        Guid ProductionProjectId,
        Guid MeasurementProjectId,
        Guid DeliveryProjectId,
        Guid SalesId,
        Guid DesignerId);
}
