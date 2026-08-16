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
            EstimatedCompletionDate = DateOnly.FromDateTime(seed.Now.AddDays(-3)),
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
            Status = ProductionRequestStatus.FEASIBLE,
            EstimatedCompletionDate = DateOnly.FromDateTime(seed.Now),
            CreatedAt = seed.Now,
            UpdatedAt = seed.Now
        });
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
        Assert.True(kpis.ReadyToComplete >= 1);
        Assert.True(kpis.OverdueTasks >= 1);
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
        Assert.Equal(3, new ProductionDashboardKpisReadModel { PendingReview = 3 }.PendingReview);
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
            Status = ProductionRequestStatus.PENDING_REVIEW,
            Priority = "HIGH",
            EstimatedCompletionDate = DateOnly.FromDateTime(now),
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
