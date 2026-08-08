using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Accounts;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class SalesWorkloadRepositoryTests
{
    [Fact]
    public async Task GetSalesWorkloadAsync_ComputesCapacityAndFuturePressure()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var items = await repository.GetSalesWorkloadAsync(
            page: 1,
            pageSize: 10,
            maxActiveProjects: 5,
            search: null,
            capacityState: null,
            futurePressureState: null,
            sortBy: "FuturePressureScoreDesc");

        var highPressure = Assert.Single(items, item => item.AccountId == data.SalesHighPressureId);
        Assert.Equal(0, highPressure.SalesActiveCount);
        Assert.Equal(5, highPressure.AvailableSlot);
        Assert.Equal(SalesWorkloadPressurePolicy.CapacityAvailableNow, highPressure.CapacityState);
        Assert.Equal(3, highPressure.ProposalConsultingCount);
        Assert.Equal(3.0m, highPressure.FuturePressureScore);
        Assert.Equal(SalesWorkloadPressurePolicy.PressureHigh, highPressure.FuturePressureState);

        var busy = Assert.Single(items, item => item.AccountId == data.SalesBusyId);
        Assert.Equal(2, busy.IntakeCount);
        Assert.Equal(1, busy.CommercialCount);
        Assert.Equal(3, busy.SalesActiveCount);
        Assert.Equal(2, busy.AvailableSlot);
        Assert.Equal(SalesWorkloadPressurePolicy.CapacityAvailableNow, busy.CapacityState);
        Assert.Equal(0.25m, busy.FuturePressureScore);
        Assert.Equal(SalesWorkloadPressurePolicy.PressureLow, busy.FuturePressureState);
    }

    [Fact]
    public async Task GetSalesWorkloadSummaryAsync_IncludesUnassignedIntake()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new AccountRepository(context);

        var summary = await repository.GetSalesWorkloadSummaryAsync(maxActiveProjects: 5);

        Assert.Equal(2, summary.TotalActiveSales);
        Assert.Equal(2, summary.AvailableNowCount);
        Assert.Equal(1, summary.HighFuturePressureCount);
        Assert.Equal(3, summary.TotalSalesActiveProjects);
        Assert.Equal(2, summary.UnassignedIntakeCount);
    }

    [Fact]
    public async Task GetUnassignedIntakeProjectsAsync_OnlySubmittedWithoutSales()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new AccountRepository(context);

        var projects = await repository.GetUnassignedIntakeProjectsAsync(page: 1, pageSize: 10);
        var count = await repository.CountUnassignedIntakeProjectsAsync();

        Assert.Equal(2, count);
        Assert.Equal(2, projects.Count);
        Assert.All(projects, project => Assert.Equal("Demo Customer", project.CustomerName));
    }

    [Fact]
    public void ResolvePressureWeight_MatchesPolicy()
    {
        Assert.Equal(1.00m, SalesWorkloadPressurePolicy.ResolvePressureWeight(ProjectStatus.PROPOSAL_CONSULTING));
        Assert.Equal(0.75m, SalesWorkloadPressurePolicy.ResolvePressureWeight(ProjectStatus.PRODUCTION_BLOCKED));
        Assert.Equal(0m, SalesWorkloadPressurePolicy.ResolvePressureWeight(ProjectStatus.IN_CONSULTATION));
        Assert.Equal(
            SalesWorkloadPressurePolicy.BucketDesignMonitor,
            SalesWorkloadPressurePolicy.ResolveBucket(ProjectStatus.PROPOSAL_CONSULTING));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(AppDbContext context)
    {
        var customerRole = CreateRole("CUSTOMER");
        var salesRole = CreateRole("SALES");
        var designerRole = CreateRole("DESIGNER");
        var customerId = Guid.NewGuid();
        var salesHighPressureId = Guid.NewGuid();
        var salesBusyId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.RoleSet.AddRange(customerRole, salesRole, designerRole);
        context.AccountSet.AddRange(
            CreateAccount(customerId, customerRole.RoleId, "customer@example.com", "Demo Customer"),
            CreateAccount(salesHighPressureId, salesRole.RoleId, "sales.a@example.com", "Sales A"),
            CreateAccount(salesBusyId, salesRole.RoleId, "sales.b@example.com", "Sales B"));

        context.ProjectSet.AddRange(
            CreateProject(customerId, null, ProjectStatus.SUBMITTED, now.AddDays(-1)),
            CreateProject(customerId, null, ProjectStatus.SUBMITTED, now.AddDays(-2)),
            CreateProject(customerId, null, ProjectStatus.IN_PRODUCTION, now.AddDays(-3)),
            CreateProject(customerId, salesHighPressureId, ProjectStatus.PROPOSAL_CONSULTING, now.AddDays(-4)),
            CreateProject(customerId, salesHighPressureId, ProjectStatus.PROPOSAL_CONSULTING, now.AddDays(-5)),
            CreateProject(customerId, salesHighPressureId, ProjectStatus.PROPOSAL_CONSULTING, now.AddDays(-6)),
            CreateProject(customerId, salesBusyId, ProjectStatus.IN_CONSULTATION, now.AddDays(-7)),
            CreateProject(customerId, salesBusyId, ProjectStatus.NEED_BASIC_INFORMATION, now.AddDays(-8)),
            CreateProject(customerId, salesBusyId, ProjectStatus.QUOTATION_SENT, now.AddDays(-9)),
            CreateProject(customerId, salesBusyId, ProjectStatus.MEASUREMENT_REQUIRED, now.AddDays(-10)),
            CreateProject(customerId, salesBusyId, ProjectStatus.COMPLETED, now.AddDays(-11)));

        await context.SaveChangesAsync();
        return new SeededData(salesHighPressureId, salesBusyId);
    }

    private static Role CreateRole(string name) => new()
    {
        RoleId = Guid.NewGuid(),
        RoleName = name,
        Description = name
    };

    private static Account CreateAccount(Guid id, Guid roleId, string email, string fullName) => new()
    {
        AccountId = id,
        RoleId = roleId,
        Email = email,
        PasswordHash = "hash",
        FullName = fullName,
        Status = AccountStatus.ACTIVE,
        CreatedAt = DateTime.UtcNow
    };

    private static Project CreateProject(
        Guid customerId,
        Guid? salesId,
        ProjectStatus status,
        DateTime assignedAt) => new()
    {
        ProjectId = Guid.NewGuid(),
        CustomerId = customerId,
        AssignedSalesId = salesId,
        ProjectCode = $"PRJ-{Guid.NewGuid():N}"[..12],
        ProjectName = $"Project {status}",
        FurnitureRequirement = "Tables",
        Status = status,
        SubmittedAt = assignedAt,
        SalesAssignedAt = salesId.HasValue ? assignedAt : null,
        CreatedAt = assignedAt
    };

    private sealed record SeededData(Guid SalesHighPressureId, Guid SalesBusyId);
}
