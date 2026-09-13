#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Dashboard;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class DashboardQueueReadRepositoryTests
{
    [Fact]
    public async Task GetSalesQueueRowsAsync_MineScope_ReturnsAssignedProjectsWithNamesAndOrder()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new DashboardQueueReadRepository(context);

        var rows = await repository.GetSalesQueueRowsAsync(new DashboardQueueFilterReadModel
        {
            Scope = "mine",
            CurrentUserId = seed.SalesId,
            CurrentUserRole = "SALES",
            UtcNow = seed.Now
        });

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.ProjectId == seed.SalesProjectId);
        var salesProject = rows.Single(row => row.ProjectId == seed.SalesProjectId);
        Assert.Equal("Customer One", salesProject.CustomerName);
        Assert.Equal("Sales One", salesProject.AssignedSalesName);
        Assert.Equal(seed.OrderId, salesProject.OrderId);
        Assert.Equal(OrderStatus.DEPOSIT_PENDING, salesProject.OrderStatus);
    }

    [Fact]
    public async Task GetSalesQueueRowsAsync_TeamAndAllScopes_FilterAssigneePresence()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new DashboardQueueReadRepository(context);

        var team = await repository.GetSalesQueueRowsAsync(new DashboardQueueFilterReadModel
        {
            Scope = "team",
            CurrentUserId = seed.SalesId,
            CurrentUserRole = "SALES",
            UtcNow = seed.Now
        });
        var adminAll = await repository.GetSalesQueueRowsAsync(new DashboardQueueFilterReadModel
        {
            Scope = "all",
            CurrentUserId = seed.AdminId,
            CurrentUserRole = "ADMIN",
            UtcNow = seed.Now
        });

        Assert.Contains(team, row => row.AssignedSalesId != null);
        Assert.True(adminAll.Count >= team.Count);
    }

    [Theory]
    [InlineData("today")]
    [InlineData("thisWeek")]
    [InlineData("thisMonth")]
    [InlineData("unknown")]
    public async Task GetSalesQueueRowsAsync_DateRangeFilters_DoNotThrow(string dateRange)
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new DashboardQueueReadRepository(context);

        var rows = await repository.GetSalesQueueRowsAsync(new DashboardQueueFilterReadModel
        {
            Scope = "mine",
            CurrentUserId = seed.SalesId,
            CurrentUserRole = "SALES",
            DateRange = dateRange,
            UtcNow = seed.Now
        });

        Assert.NotNull(rows);
    }

    [Fact]
    public async Task GetSalesKpisAsync_AggregatesCounts()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new DashboardQueueReadRepository(context);

        var kpis = await repository.GetSalesKpisAsync(new DashboardQueueFilterReadModel
        {
            Scope = "mine",
            CurrentUserId = seed.SalesId,
            CurrentUserRole = "SALES",
            UtcNow = seed.Now
        });

        Assert.True(kpis.ActiveProjects >= 1);
        Assert.True(kpis.PaymentFollowUp >= 1);
    }

    [Fact]
    public async Task GetDesignerQueueRowsAndKpis_ReturnDesignActiveWork()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new DashboardQueueReadRepository(context);
        var filter = new DashboardQueueFilterReadModel
        {
            Scope = "mine",
            CurrentUserId = seed.DesignerId,
            CurrentUserRole = "DESIGNER",
            UtcNow = seed.Now
        };

        var rows = await repository.GetDesignerQueueRowsAsync(filter);
        var kpis = await repository.GetDesignerKpisAsync(filter);

        Assert.Contains(rows, row => row.ProjectId == seed.DesignerProjectId);
        Assert.True(kpis.MeasurementDue >= 1);
    }

    [Fact]
    public async Task GetDesignerQueueRowsAsync_TeamScope_RequiresAssignedDesigner()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new DashboardQueueReadRepository(context);

        var rows = await repository.GetDesignerQueueRowsAsync(new DashboardQueueFilterReadModel
        {
            Scope = "team",
            CurrentUserId = seed.DesignerId,
            CurrentUserRole = "DESIGNER",
            UtcNow = seed.Now
        });

        Assert.All(rows, row => Assert.NotNull(row.AssignedDesignerId));
    }

    [Fact]
    public async Task GetProductionQueueRowsAndKpis_MineScope_FiltersAssignee()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new DashboardQueueReadRepository(context);
        var filter = new DashboardQueueFilterReadModel
        {
            Scope = "mine",
            CurrentUserId = seed.ProductionId,
            CurrentUserRole = "PRODUCTION",
            UtcNow = seed.Now
        };

        var rows = await repository.GetProductionQueueRowsAsync(filter);
        var kpis = await repository.GetProductionKpisAsync(filter);

        Assert.Single(rows);
        Assert.Equal(seed.ProductionRequestId, rows[0].ProductionRequestId);
        Assert.Equal("Customer One", rows[0].CustomerName);
        Assert.Equal("Production One", rows[0].AssignedToName);
        Assert.Equal(1, rows[0].BlockedItemCount);
        Assert.True(kpis.PendingReview >= 1);
    }

    [Theory]
    [InlineData("today")]
    [InlineData("thisWeek")]
    [InlineData("thisMonth")]
    [InlineData(null)]
    public async Task GetProductionQueueRowsAsync_DateRanges_DoNotThrow(string? dateRange)
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var repository = new DashboardQueueReadRepository(context);

        var rows = await repository.GetProductionQueueRowsAsync(new DashboardQueueFilterReadModel
        {
            Scope = "team",
            CurrentUserId = seed.ProductionId,
            CurrentUserRole = "PRODUCTION",
            DateRange = dateRange,
            UtcNow = seed.Now
        });

        Assert.NotEmpty(rows);
    }

    [Fact]
    public async Task GetProductionKpisAsync_CountsInProductionAndOverdue()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        context.ProductionRequestSet.Add(new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProductionCode = "PR-IN",
            ProjectId = seed.SalesProjectId,
            OrderId = seed.OrderId,
            AssignedTo = seed.ProductionId,
            Status = ProductionRequestStatus.IN_PRODUCTION,
            CreatedAt = seed.Now,
            UpdatedAt = seed.Now
        });
        context.ProductionRequestSet.Add(new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProductionCode = "PR-FEAS",
            ProjectId = seed.SalesProjectId,
            OrderId = seed.OrderId,
            AssignedTo = seed.ProductionId,
            Status = ProductionRequestStatus.PENDING,
            CreatedAt = seed.Now,
            UpdatedAt = seed.Now
        });
        context.ProjectPhaseTimelineSet.Add(
            CreatePhaseDeadline(seed.SalesProjectId, ProjectPhaseType.PRODUCTION, DateOnly.FromDateTime(seed.Now.AddDays(-3)), seed.SalesId, seed.Now));
        await context.SaveChangesAsync();
        var repository = new DashboardQueueReadRepository(context);

        var kpis = await repository.GetProductionKpisAsync(new DashboardQueueFilterReadModel
        {
            Scope = "mine",
            CurrentUserId = seed.ProductionId,
            CurrentUserRole = "PRODUCTION",
            UtcNow = seed.Now
        });

        Assert.True(kpis.InProduction >= 1);
        Assert.True(kpis.PendingReview >= 1);
        Assert.Equal(kpis.PendingStart, kpis.PendingReview);
        Assert.Equal(0, kpis.ReadyToComplete);
        Assert.True(kpis.OverdueTasks >= 1);
        Assert.Equal(0, kpis.PendingCustomizationReview);
    }

    [Fact]
    public void ReadModels_PropertyCoverage()
    {
        var filter = new DashboardQueueFilterReadModel
        {
            Scope = "all",
            CurrentUserId = Guid.NewGuid(),
            CurrentUserRole = "ADMIN",
            Search = "x",
            DateRange = "today",
            UtcNow = DateTime.UtcNow
        };
        Assert.Equal("all", filter.Scope);
        Assert.Equal(1, new SalesDashboardKpisReadModel { NewRequests = 1 }.NewRequests);
        Assert.Equal(2, new DesignerDashboardKpisReadModel { MeasurementDue = 2 }.MeasurementDue);
        Assert.Equal(3, new ProductionDashboardKpisReadModel
        {
            PendingCustomizationReview = 1,
            PendingStart = 2,
            PendingReview = 3,
            ReadyForDelivery = 4,
            AwaitingDeliverySchedule = 5,
            CompletedInRange = 6
        }.PendingReview);

        var customization = new DashboardProductionCustomizationQueueRowReadModel
        {
            VersionId = Guid.NewGuid(),
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ProjectCode = "P",
            ProjectName = "N",
            CustomerName = "C",
            VersionTitle = "V1",
            MaterialAvailable = true,
            SubmittedForReviewAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var delivery = new DashboardProductionDeliveryQueueRowReadModel
        {
            OrderId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ProjectCode = "P",
            ProjectName = "N",
            CustomerName = "C",
            ProductionRequestId = Guid.NewGuid(),
            AssignedTo = Guid.NewGuid(),
            AssignedToName = "Prod",
            OrderStatus = OrderStatus.READY_FOR_DELIVERY,
            DeliveryQueueStatus = "AWAITING_SCHEDULE",
            ScheduledEnd = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        var deadlineQuery = new ProjectPhaseDeadlineRiskQueryReadModel
        {
            Phase = ProjectPhaseType.PRODUCTION,
            ProductionId = Guid.NewGuid(),
            SalesId = Guid.NewGuid(),
            DesignerId = Guid.NewGuid()
        };

        Assert.Equal("V1", customization.VersionTitle);
        Assert.Equal("AWAITING_SCHEDULE", delivery.DeliveryQueueStatus);
        Assert.NotNull(deadlineQuery.ProductionId);
    }

    [Fact]
    public async Task GetProductionKpisAsync_ScopeAll_CountsCustomizationReadyDeliveryAndCompleted()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var readyRequestId = Guid.NewGuid();
        var completedRequestId = Guid.NewGuid();
        var deliveryOrderId = Guid.NewGuid();
        var customizationRequestId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();

        context.OrderSet.Add(new Order
        {
            OrderId = deliveryOrderId,
            ProjectId = seed.SalesProjectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-DEL",
            CustomerId = Guid.NewGuid(),
            SalesId = seed.SalesId,
            FinalTotalAmount = 1000m,
            Status = OrderStatus.READY_FOR_DELIVERY,
            CreatedAt = seed.Now,
            UpdatedAt = seed.Now
        });
        context.ProductionRequestSet.AddRange(
            new ProductionRequest
            {
                ProductionRequestId = readyRequestId,
                ProductionCode = "PR-READY",
                ProjectId = seed.SalesProjectId,
                OrderId = seed.OrderId,
                AssignedTo = seed.ProductionId,
                Status = ProductionRequestStatus.IN_PRODUCTION,
                CreatedAt = seed.Now,
                UpdatedAt = seed.Now
            },
            new ProductionRequest
            {
                ProductionRequestId = completedRequestId,
                ProductionCode = "PR-DONE",
                ProjectId = seed.SalesProjectId,
                OrderId = seed.OrderId,
                AssignedTo = seed.ProductionId,
                Status = ProductionRequestStatus.COMPLETED,
                ActualCompletionDate = DateOnly.FromDateTime(seed.Now),
                CreatedAt = seed.Now,
                UpdatedAt = seed.Now
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProductionCode = "PR-DEL-OWN",
                ProjectId = seed.SalesProjectId,
                OrderId = deliveryOrderId,
                AssignedTo = seed.ProductionId,
                Status = ProductionRequestStatus.COMPLETED,
                ActualCompletionDate = DateOnly.FromDateTime(seed.Now.AddDays(-10)),
                CreatedAt = seed.Now,
                UpdatedAt = seed.Now
            });
        context.ProductionItemSet.AddRange(
            new ProductionItem
            {
                ProductionItemId = Guid.NewGuid(),
                ProductionRequestId = readyRequestId,
                OrderItemId = Guid.NewGuid(),
                Status = ProductionItemStatus.COMPLETED
            },
            new ProductionItem
            {
                ProductionItemId = Guid.NewGuid(),
                ProductionRequestId = readyRequestId,
                OrderItemId = Guid.NewGuid(),
                Status = ProductionItemStatus.CANCELLED
            });
        context.CustomizationRequestSet.Add(new CustomizationRequest
        {
            CustomizationRequestId = customizationRequestId,
            ProjectId = seed.SalesProjectId,
            ProposalId = Guid.NewGuid(),
            SourceProductVersionId = productVersionId,
            RequestTitle = "Custom table",
            Status = CustomizationStatus.REVIEWING,
            CreatedAt = seed.Now,
            UpdatedAt = seed.Now
        });
        context.CustomizationRequestVersionSet.Add(new CustomizationRequestVersion
        {
            CustomizationRequestVersionId = Guid.NewGuid(),
            CustomizationRequestId = customizationRequestId,
            ProductVersionId = productVersionId,
            VersionNo = 1,
            CreatedByDesignerId = seed.DesignerId,
            Status = CustomizationVersionStatus.REVIEWING,
            FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
            SubmittedForReviewAt = seed.Now,
            CreatedAt = seed.Now,
            UpdatedAt = seed.Now
        });
        await context.SaveChangesAsync();

        var repository = new DashboardQueueReadRepository(context);
        var allFilter = new DashboardQueueFilterReadModel
        {
            Scope = "all",
            CurrentUserId = seed.ProductionId,
            CurrentUserRole = "PRODUCTION",
            DateRange = "today",
            UtcNow = seed.Now
        };
        var mineFilter = new DashboardQueueFilterReadModel
        {
            Scope = "mine",
            CurrentUserId = seed.ProductionId,
            CurrentUserRole = "PRODUCTION",
            DateRange = "today",
            UtcNow = seed.Now
        };

        var allKpis = await repository.GetProductionKpisAsync(allFilter);
        var mineKpis = await repository.GetProductionKpisAsync(mineFilter);
        var customizationRows = await repository.GetProductionCustomizationQueueRowsAsync(allFilter);
        var emptyCustomization = await repository.GetProductionCustomizationQueueRowsAsync(mineFilter);
        var deliveryRows = await repository.GetProductionDeliveryQueueRowsAsync(mineFilter);

        Assert.True(allKpis.PendingCustomizationReview >= 1);
        Assert.Equal(0, mineKpis.PendingCustomizationReview);
        Assert.True(allKpis.ReadyToComplete >= 1);
        Assert.True(allKpis.ReadyForDelivery >= 1);
        Assert.True(allKpis.AwaitingDeliverySchedule >= 1);
        Assert.True(allKpis.CompletedInRange >= 1);
        Assert.NotEmpty(customizationRows);
        Assert.Empty(emptyCustomization);
        Assert.NotEmpty(deliveryRows);
        Assert.Equal("AWAITING_SCHEDULE", deliveryRows[0].DeliveryQueueStatus);
    }

    [Fact]
    public async Task GetProductionDeliveryQueueRowsAsync_ResolvesScheduledInProgressAndAwaitConfirm()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var scheduledOrderId = Guid.NewGuid();
        var inProgressOrderId = Guid.NewGuid();
        var awaitConfirmOrderId = Guid.NewGuid();

        context.OrderSet.AddRange(
            new Order
            {
                OrderId = scheduledOrderId,
                ProjectId = seed.SalesProjectId,
                QuotationId = Guid.NewGuid(),
                OrderCode = "ORD-SCH",
                CustomerId = Guid.NewGuid(),
                FinalTotalAmount = 1m,
                Status = OrderStatus.READY_FOR_DELIVERY,
                CreatedAt = seed.Now,
                UpdatedAt = seed.Now
            },
            new Order
            {
                OrderId = inProgressOrderId,
                ProjectId = seed.SalesProjectId,
                QuotationId = Guid.NewGuid(),
                OrderCode = "ORD-IP",
                CustomerId = Guid.NewGuid(),
                FinalTotalAmount = 1m,
                Status = OrderStatus.DELIVERING,
                CreatedAt = seed.Now,
                UpdatedAt = seed.Now
            },
            new Order
            {
                OrderId = awaitConfirmOrderId,
                ProjectId = seed.SalesProjectId,
                QuotationId = Guid.NewGuid(),
                OrderCode = "ORD-AC",
                CustomerId = Guid.NewGuid(),
                FinalTotalAmount = 1m,
                Status = OrderStatus.AWAITING_CUSTOMER_CONFIRMATION,
                CreatedAt = seed.Now,
                UpdatedAt = seed.Now
            });
        context.ProductionRequestSet.AddRange(
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = seed.SalesProjectId,
                OrderId = scheduledOrderId,
                AssignedTo = seed.ProductionId,
                Status = ProductionRequestStatus.COMPLETED,
                CreatedAt = seed.Now,
                UpdatedAt = seed.Now
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = seed.SalesProjectId,
                OrderId = inProgressOrderId,
                AssignedTo = seed.ProductionId,
                Status = ProductionRequestStatus.COMPLETED,
                CreatedAt = seed.Now,
                UpdatedAt = seed.Now
            },
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                ProjectId = seed.SalesProjectId,
                OrderId = awaitConfirmOrderId,
                AssignedTo = seed.ProductionId,
                Status = ProductionRequestStatus.COMPLETED,
                CreatedAt = seed.Now,
                UpdatedAt = seed.Now
            });
        context.ProjectScheduleSet.Add(new ProjectSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = seed.SalesProjectId,
            ScheduleType = ProjectScheduleType.DELIVERY,
            Title = "Delivery",
            ScheduledStart = seed.Now,
            ScheduledEnd = seed.Now.AddDays(2),
            Status = ProjectScheduleStatus.CONFIRMED,
            CreatedAt = seed.Now,
            UpdatedAt = seed.Now
        });
        context.DeliverySet.Add(new Delivery
        {
            DeliveryId = Guid.NewGuid(),
            OrderId = inProgressOrderId,
            Status = DeliveryStatus.IN_PROGRESS,
            CreatedAt = seed.Now,
            UpdatedAt = seed.Now
        });
        await context.SaveChangesAsync();

        var repository = new DashboardQueueReadRepository(context);
        var rows = await repository.GetProductionDeliveryQueueRowsAsync(new DashboardQueueFilterReadModel
        {
            Scope = "mine",
            CurrentUserId = seed.ProductionId,
            CurrentUserRole = "PRODUCTION",
            UtcNow = seed.Now
        });

        Assert.Contains(rows, row => row.OrderId == scheduledOrderId && row.DeliveryQueueStatus == "SCHEDULED");
        Assert.Contains(rows, row => row.OrderId == inProgressOrderId && row.DeliveryQueueStatus == "IN_PROGRESS");
        Assert.Contains(rows, row =>
            row.OrderId == awaitConfirmOrderId &&
            row.DeliveryQueueStatus == "AWAITING_CUSTOMER_CONFIRMATION");
    }

    [Fact]
    public async Task GetProjectPhaseDeadlineRiskRowsAsync_FiltersByProductionId()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        context.ProjectPhaseTimelineSet.Add(
            CreatePhaseDeadline(
                seed.SalesProjectId,
                ProjectPhaseType.PRODUCTION,
                DateOnly.FromDateTime(seed.Now.AddDays(5)),
                seed.SalesId,
                seed.Now));
        await context.SaveChangesAsync();
        var repository = new DashboardQueueReadRepository(context);

        var matched = await repository.GetProjectPhaseDeadlineRiskRowsAsync(
            new ProjectPhaseDeadlineRiskQueryReadModel
            {
                Phase = ProjectPhaseType.PRODUCTION,
                ProductionId = seed.ProductionId
            });
        var missed = await repository.GetProjectPhaseDeadlineRiskRowsAsync(
            new ProjectPhaseDeadlineRiskQueryReadModel
            {
                Phase = ProjectPhaseType.PRODUCTION,
                ProductionId = Guid.NewGuid()
            });

        Assert.NotEmpty(matched);
        Assert.Empty(missed);
    }

    [Fact]
    public async Task GetProjectPhaseDeadlineRiskRowsAsync_ReturnsDeadlineContext()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        context.ProjectPhaseTimelineSet.AddRange(
            CreatePhaseDeadline(seed.SalesProjectId, ProjectPhaseType.PROPOSAL, new DateOnly(2026, 8, 15), seed.SalesId, seed.Now),
            CreatePhaseDeadline(seed.SalesProjectId, ProjectPhaseType.PRODUCTION, new DateOnly(2026, 8, 25), seed.SalesId, seed.Now));
        await context.SaveChangesAsync();
        var repository = new DashboardQueueReadRepository(context);

        var rows = await repository.GetProjectPhaseDeadlineRiskRowsAsync(new ProjectPhaseDeadlineRiskQueryReadModel
        {
            Phase = ProjectPhaseType.PROPOSAL,
            SalesId = seed.SalesId,
            DesignerId = seed.DesignerId,
            From = new DateOnly(2026, 8, 1),
            To = new DateOnly(2026, 8, 20)
        });

        Assert.Single(rows);
        Assert.Equal(seed.SalesProjectId, rows[0].ProjectId);
        Assert.Equal(ProjectPhaseType.PROPOSAL, rows[0].Phase);
        Assert.Equal("Sales One", rows[0].AssignedSalesName);
        Assert.Equal("Designer One", rows[0].AssignedDesignerName);
        Assert.Equal(seed.ProductionId, rows[0].AssignedProductionId);
        Assert.Equal("Production One", rows[0].AssignedProductionName);
    }

    [Fact]
    public async Task GetProjectPhaseDeadlineRiskRowsAsync_DateFilterExcludesOutsideRange()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        context.ProjectPhaseTimelineSet.Add(
            CreatePhaseDeadline(seed.DesignerProjectId, ProjectPhaseType.PRODUCTION, new DateOnly(2026, 9, 20), seed.SalesId, seed.Now));
        await context.SaveChangesAsync();
        var repository = new DashboardQueueReadRepository(context);

        var rows = await repository.GetProjectPhaseDeadlineRiskRowsAsync(new ProjectPhaseDeadlineRiskQueryReadModel
        {
            From = new DateOnly(2026, 8, 1),
            To = new DateOnly(2026, 8, 31)
        });

        Assert.Empty(rows);
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
        var now = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
        var customerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER" };
        var salesRole = new Role { RoleId = Guid.NewGuid(), RoleName = "SALES" };
        var designerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "DESIGNER" };
        var productionRole = new Role { RoleId = Guid.NewGuid(), RoleName = "PRODUCTION" };
        var adminRole = new Role { RoleId = Guid.NewGuid(), RoleName = "ADMIN" };

        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productionId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var salesProjectId = Guid.NewGuid();
        var designerProjectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var productionRequestId = Guid.NewGuid();

        context.RoleSet.AddRange(customerRole, salesRole, designerRole, productionRole, adminRole);
        context.AccountSet.AddRange(
            CreateAccount(customerId, customerRole.RoleId, "customer@example.com", "Customer One"),
            CreateAccount(salesId, salesRole.RoleId, "sales@example.com", "Sales One"),
            CreateAccount(designerId, designerRole.RoleId, "designer@example.com", "Designer One"),
            CreateAccount(productionId, productionRole.RoleId, "production@example.com", "Production One"),
            CreateAccount(adminId, adminRole.RoleId, "admin@example.com", "Admin One"));

        context.ProjectSet.AddRange(
            new Project
            {
                ProjectId = salesProjectId,
                CustomerId = customerId,
                AssignedSalesId = salesId,
                AssignedDesignerId = designerId,
                ProjectCode = "PRJ-SALES",
                ProjectName = "Sales Project",
                Status = ProjectStatus.ORDER_CONFIRMED,
                TargetCompletionDate = DateOnly.FromDateTime(now),
                SubmittedAt = now.AddDays(-10),
                CreatedAt = now.AddDays(-11),
                UpdatedAt = now.AddDays(-1)
            },
            new Project
            {
                ProjectId = designerProjectId,
                CustomerId = customerId,
                AssignedSalesId = salesId,
                AssignedDesignerId = designerId,
                ProjectCode = "PRJ-DESIGN",
                ProjectName = "Designer Project",
                Status = ProjectStatus.MEASUREMENT_REQUIRED,
                TargetCompletionDate = DateOnly.FromDateTime(now.AddDays(-1)),
                SubmittedAt = now.AddDays(-5),
                CreatedAt = now.AddDays(-6),
                UpdatedAt = now.AddDays(-2)
            },
            new Project
            {
                ProjectId = Guid.NewGuid(),
                CustomerId = customerId,
                ProjectCode = "PRJ-UNASSIGNED",
                ProjectName = "Unassigned",
                Status = ProjectStatus.SUBMITTED,
                CreatedAt = now,
                UpdatedAt = now
            });

        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = salesProjectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-1",
            CustomerId = customerId,
            SalesId = salesId,
            FinalTotalAmount = 1000m,
            PaidAmount = 300m,
            RemainingAmount = 700m,
            Status = OrderStatus.DEPOSIT_PENDING,
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now.AddDays(-1)
        });

        context.ProductionRequestSet.Add(new ProductionRequest
        {
            ProductionRequestId = productionRequestId,
            ProductionCode = "PR-1",
            ProjectId = salesProjectId,
            OrderId = orderId,
            AssignedTo = productionId,
            Status = ProductionRequestStatus.PENDING,
            Priority = "HIGH",
            CreatedAt = now,
            UpdatedAt = now
        });

        context.ProductionItemSet.Add(new ProductionItem
        {
            ProductionItemId = Guid.NewGuid(),
            ProductionRequestId = productionRequestId,
            OrderItemId = Guid.NewGuid(),
            Status = ProductionItemStatus.CANCELLED,
            ProductNameSnapshot = "Chair"
        });

        await context.SaveChangesAsync();

        return new SeedData(
            salesId,
            designerId,
            productionId,
            adminId,
            salesProjectId,
            designerProjectId,
            orderId,
            productionRequestId,
            now);
    }

    private static Account CreateAccount(Guid accountId, Guid roleId, string email, string fullName)
    {
        return new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = fullName,
            Status = AccountStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static ProjectPhaseTimeline CreatePhaseDeadline(
        Guid projectId,
        ProjectPhaseType phase,
        DateOnly dueDate,
        Guid createdBy,
        DateTime now)
    {
        return new ProjectPhaseTimeline
        {
            ProjectPhaseTimelineId = Guid.NewGuid(),
            ProjectId = projectId,
            Phase = phase,
            DueDate = dueDate,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private sealed record SeedData(
        Guid SalesId,
        Guid DesignerId,
        Guid ProductionId,
        Guid AdminId,
        Guid SalesProjectId,
        Guid DesignerProjectId,
        Guid OrderId,
        Guid ProductionRequestId,
        DateTime Now);
}
