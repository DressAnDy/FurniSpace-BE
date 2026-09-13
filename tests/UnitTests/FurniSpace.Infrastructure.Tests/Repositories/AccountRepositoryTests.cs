using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Accounts;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class AccountRepositoryTests
{
    [Fact]
    public async Task GetDetailAsync_ReturnsJoinedRoleProjection()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var detail = await repository.GetDetailAsync(data.CustomerId);

        Assert.NotNull(detail);
        Assert.Equal(data.CustomerId, detail.AccountId);
        Assert.Equal("customer@example.com", detail.Email);
        Assert.Equal("Customer User", detail.FullName);
        Assert.Equal("CUSTOMER", detail.Role.RoleName);
        Assert.Equal(AccountStatus.ACTIVE, detail.Status);
    }

    [Fact]
    public async Task CountGroupedFacets_ExcludeDeletedByDefault()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var statusCounts = await repository.CountGroupedByStatusAsync(includeDeleted: false);
        var roleCounts = await repository.CountGroupedByRoleIdAsync(includeDeleted: false);
        var statusCountsWithDeleted = await repository.CountGroupedByStatusAsync(includeDeleted: true);

        Assert.Contains(statusCounts, facet => facet.Key == "ACTIVE" && facet.Count == 7);
        Assert.Contains(statusCounts, facet => facet.Key == "INACTIVE" && facet.Count == 1);
        Assert.Contains(statusCounts, facet => facet.Key == "SUSPENDED" && facet.Count == 1);
        Assert.Contains(roleCounts, facet => facet.Key == data.DesignerRoleId.ToString() && facet.Count == 7);
        Assert.Contains(roleCounts, facet => facet.Key == data.CustomerRoleId.ToString() && facet.Count == 1);
        Assert.Contains(statusCountsWithDeleted, facet => facet.Key == "ACTIVE" && facet.Count == 8);
    }

    [Fact]
    public async Task GetAvailableDesignersAsync_UsesDesignActiveCountForCapacityAndOrdersByLoad()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var designers = await repository.GetAvailableDesignersAsync(
            page: 1,
            pageSize: 10,
            maxActiveProjects: 2,
            search: null);
        var count = await repository.CountAvailableDesignersAsync(
            maxActiveProjects: 2,
            search: null);

        Assert.Equal(5, count);
        Assert.Equal(
            [
                data.DesignerWithoutProjectsId,
                data.DesignerPostDesignOnlyId,
                data.DesignerWithOneProjectId,
                data.DesignerAtCapacityId,
                data.DesignerOverCapacityId
            ],
            designers.Select(designer => designer.AccountId));

        var idle = Assert.Single(designers, designer => designer.AccountId == data.DesignerWithoutProjectsId);
        Assert.Equal(0, idle.DesignActiveCount);
        Assert.Equal(0, idle.LifecycleAssignedCount);
        Assert.Equal(0, idle.CurrentActiveProjectCount);
        Assert.Equal(2, idle.AvailableSlot);
        Assert.Equal(DesignerWorkloadStatusSets.CapacityAvailable, idle.CapacityState);

        var postDesignOnly = Assert.Single(designers, designer => designer.AccountId == data.DesignerPostDesignOnlyId);
        Assert.Equal(0, postDesignOnly.DesignActiveCount);
        Assert.Equal(1, postDesignOnly.LifecycleAssignedCount);
        Assert.Equal(2, postDesignOnly.AvailableSlot);
        Assert.Equal(DesignerWorkloadStatusSets.CapacityAvailable, postDesignOnly.CapacityState);

        var oneActive = Assert.Single(designers, designer => designer.AccountId == data.DesignerWithOneProjectId);
        Assert.Equal(1, oneActive.DesignActiveCount);
        Assert.Equal(1, oneActive.LifecycleAssignedCount);
        Assert.Equal(1, oneActive.AvailableSlot);
        Assert.Equal(DesignerWorkloadStatusSets.CapacityAvailable, oneActive.CapacityState);

        Assert.Contains(designers, designer =>
            designer.AccountId == data.DesignerAtCapacityId &&
            designer.CapacityState == DesignerWorkloadStatusSets.CapacityFull);
        Assert.Contains(designers, designer =>
            designer.AccountId == data.DesignerOverCapacityId &&
            designer.CapacityState == DesignerWorkloadStatusSets.CapacityOver);
    }

    [Fact]
    public async Task GetDesignerWorkloadAsync_FiltersByCapacityStateAndMarksFullAndOver()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var full = await repository.GetDesignerWorkloadAsync(
            page: 1,
            pageSize: 10,
            maxActiveProjects: 2,
            search: null,
            capacityState: DesignerWorkloadStatusSets.CapacityFull,
            sortBy: "DesignActiveCountDesc");
        var over = await repository.GetDesignerWorkloadAsync(
            page: 1,
            pageSize: 10,
            maxActiveProjects: 2,
            search: null,
            capacityState: DesignerWorkloadStatusSets.CapacityOver,
            sortBy: "DesignActiveCountDesc");

        var fullDesigner = Assert.Single(full);
        Assert.Equal(data.DesignerAtCapacityId, fullDesigner.AccountId);
        Assert.Equal(2, fullDesigner.DesignActiveCount);
        Assert.Equal(0, fullDesigner.AvailableSlot);
        Assert.Equal(DesignerWorkloadStatusSets.CapacityFull, fullDesigner.CapacityState);

        var overDesigner = Assert.Single(over);
        Assert.Equal(data.DesignerOverCapacityId, overDesigner.AccountId);
        Assert.Equal(3, overDesigner.DesignActiveCount);
        Assert.Equal(-1, overDesigner.AvailableSlot);
        Assert.Equal(DesignerWorkloadStatusSets.CapacityOver, overDesigner.CapacityState);
    }

    [Fact]
    public async Task GetDesignerWorkloadSummaryAsync_AggregatesCapacityStates()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new AccountRepository(context);

        var summary = await repository.GetDesignerWorkloadSummaryAsync(maxActiveProjects: 2);

        Assert.Equal(5, summary.TotalActiveDesigners);
        Assert.Equal(3, summary.AvailableCount);
        Assert.Equal(1, summary.FullCount);
        Assert.Equal(1, summary.OverCount);
        Assert.Equal(6, summary.TotalDesignActiveProjects);
    }

    [Fact]
    public async Task GetDesignerAssignedProjectsAsync_ReturnsBucketFilterableProjects()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var all = await repository.GetDesignerAssignedProjectsAsync(
            data.DesignerOverCapacityId,
            page: 1,
            pageSize: 10,
            bucket: null);
        var designActive = await repository.GetDesignerAssignedProjectsAsync(
            data.DesignerOverCapacityId,
            page: 1,
            pageSize: 10,
            bucket: DesignerWorkloadStatusSets.BucketDesignActive);

        Assert.Equal(3, all.Count);
        Assert.Equal(3, designActive.Count);
        Assert.All(designActive, project =>
            Assert.Contains(project.Status!.Value, DesignerWorkloadStatusSets.DesignActive));
        Assert.Contains(all, project => project.CustomerName == "Customer User");
    }

    [Fact]
    public async Task GetDesignerWorkloadAsync_SortsByAvailableSlotDesc()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new AccountRepository(context);

        var items = await repository.GetDesignerWorkloadAsync(
            page: 1,
            pageSize: 20,
            maxActiveProjects: 2,
            search: null,
            capacityState: null,
            sortBy: "AvailableSlotDesc");

        Assert.True(items.Count >= 2);
        Assert.True(items[0].AvailableSlot >= items[^1].AvailableSlot);
    }

    [Fact]
    public async Task GetDesignerAssignedProjectsAsync_FiltersPostDesignAndTerminalBuckets()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var postDesign = await repository.GetDesignerAssignedProjectsAsync(
            data.DesignerPostDesignOnlyId,
            page: 1,
            pageSize: 10,
            bucket: DesignerWorkloadStatusSets.BucketPostDesign);
        Assert.NotEmpty(postDesign);
        Assert.All(postDesign, project =>
            Assert.Contains(project.Status!.Value, DesignerWorkloadStatusSets.PostDesign));

        var terminalCount = await repository.CountDesignerAssignedProjectsAsync(
            data.DesignerOverCapacityId,
            bucket: DesignerWorkloadStatusSets.BucketTerminal);
        Assert.Equal(0, terminalCount);

        Assert.True(await repository.IsActiveDesignerAsync(data.DesignerOverCapacityId));
        Assert.False(await repository.IsActiveDesignerAsync(Guid.NewGuid()));
    }

    [Fact]
    public void BuildSearchPattern_TrimsInputAndWrapsWithWildcards()
    {
        var method = typeof(AccountRepository).GetMethod(
            "BuildSearchPattern",
            BindingFlags.NonPublic | BindingFlags.Static);

        var pattern = method!.Invoke(null, ["  emily  "]);

        Assert.Equal("%emily%", pattern);
    }

    [Fact]
    public void ResolveBucket_MapsStatusesCorrectly()
    {
        Assert.Equal(
            DesignerWorkloadStatusSets.BucketDesignActive,
            DesignerWorkloadStatusSets.ResolveBucket(ProjectStatus.PROPOSAL_CONSULTING));
        Assert.Equal(
            DesignerWorkloadStatusSets.BucketPostDesign,
            DesignerWorkloadStatusSets.ResolveBucket(ProjectStatus.QUOTATION_REVISION_REQUESTED));
        Assert.Equal(
            DesignerWorkloadStatusSets.BucketTerminal,
            DesignerWorkloadStatusSets.ResolveBucket(ProjectStatus.COMPLETED));
        Assert.Equal(
            DesignerWorkloadStatusSets.BucketOther,
            DesignerWorkloadStatusSets.ResolveBucket(ProjectStatus.IN_CONSULTATION));
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
        var customerRole = CreateRole("CUSTOMER", "Customer accounts");
        var salesRole = CreateRole("SALES", "Sales accounts");
        var designerRole = CreateRole("DESIGNER", "Designer accounts");
        var adminRole = CreateRole("ADMIN", "Admin accounts");

        var customerId = Guid.NewGuid();
        var deletedCustomerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerWithoutProjectsId = Guid.NewGuid();
        var designerPostDesignOnlyId = Guid.NewGuid();
        var designerWithOneProjectId = Guid.NewGuid();
        var designerAtCapacityId = Guid.NewGuid();
        var designerOverCapacityId = Guid.NewGuid();
        var suspendedDesignerId = Guid.NewGuid();
        var inactiveDesignerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.RoleSet.AddRange(customerRole, salesRole, designerRole, adminRole);
        context.AccountSet.AddRange(
            CreateAccount(customerId, customerRole.RoleId, "customer@example.com", "Customer User", AccountStatus.ACTIVE, now.AddDays(-1)),
            CreateAccount(deletedCustomerId, customerRole.RoleId, "deleted@example.com", "Deleted User", AccountStatus.ACTIVE, now.AddDays(-2), deletedAt: now),
            CreateAccount(salesId, salesRole.RoleId, "sales@example.com", "Sales User", AccountStatus.ACTIVE, now.AddDays(-3)),
            CreateAccount(designerWithoutProjectsId, designerRole.RoleId, "amy.designer@example.com", "Amy Designer", AccountStatus.ACTIVE, now.AddDays(-4)),
            CreateAccount(designerPostDesignOnlyId, designerRole.RoleId, "zoe.designer@example.com", "Zoe Designer", AccountStatus.ACTIVE, now.AddDays(-4).AddHours(1)),
            CreateAccount(designerWithOneProjectId, designerRole.RoleId, "beth.designer@example.com", "Beth Designer", AccountStatus.ACTIVE, now.AddDays(-5)),
            CreateAccount(designerAtCapacityId, designerRole.RoleId, "cara.designer@example.com", "Cara Designer", AccountStatus.ACTIVE, now.AddDays(-6)),
            CreateAccount(designerOverCapacityId, designerRole.RoleId, "erin.designer@example.com", "Erin Designer", AccountStatus.ACTIVE, now.AddDays(-6).AddHours(1)),
            CreateAccount(suspendedDesignerId, designerRole.RoleId, "suspended.designer@example.com", "Suspended Designer", AccountStatus.SUSPENDED, now.AddDays(-6).AddHours(2)),
            CreateAccount(inactiveDesignerId, designerRole.RoleId, "dana.designer@example.com", "Dana Designer", AccountStatus.INACTIVE, now.AddDays(-7)));

        context.ProjectSet.AddRange(
            CreateProject(customerId, salesId, designerWithoutProjectsId, ProjectStatus.COMPLETED, now.AddDays(-20)),
            CreateProject(customerId, salesId, designerPostDesignOnlyId, ProjectStatus.IN_PRODUCTION, now.AddDays(-10)),
            CreateProject(customerId, salesId, designerWithOneProjectId, ProjectStatus.PROPOSAL_CONSULTING, now.AddDays(-8)),
            CreateProject(customerId, salesId, designerAtCapacityId, ProjectStatus.MEASUREMENT_REQUIRED, now.AddDays(-7)),
            CreateProject(customerId, salesId, designerAtCapacityId, ProjectStatus.SPACE_VERIFIED, now.AddDays(-6)),
            CreateProject(customerId, salesId, designerOverCapacityId, ProjectStatus.MEASUREMENT_REQUIRED, now.AddDays(-5)),
            CreateProject(customerId, salesId, designerOverCapacityId, ProjectStatus.SPACE_VERIFIED, now.AddDays(-4)),
            CreateProject(customerId, salesId, designerOverCapacityId, ProjectStatus.PROPOSAL_CONSULTING, now.AddDays(-3)),
            CreateProject(customerId, salesId, suspendedDesignerId, ProjectStatus.PROPOSAL_CONSULTING, now.AddDays(-2)));

        await context.SaveChangesAsync();

        return new SeededData(
            CustomerId: customerId,
            DesignerWithoutProjectsId: designerWithoutProjectsId,
            DesignerPostDesignOnlyId: designerPostDesignOnlyId,
            DesignerWithOneProjectId: designerWithOneProjectId,
            DesignerAtCapacityId: designerAtCapacityId,
            DesignerOverCapacityId: designerOverCapacityId,
            DesignerRoleId: designerRole.RoleId,
            SalesRoleId: salesRole.RoleId,
            CustomerRoleId: customerRole.RoleId);
    }

    private static Role CreateRole(string roleName, string description)
    {
        return new Role
        {
            RoleId = Guid.NewGuid(),
            RoleName = roleName,
            Description = description
        };
    }

    private static Account CreateAccount(
        Guid accountId,
        Guid roleId,
        string email,
        string fullName,
        AccountStatus status,
        DateTime createdAt,
        DateTime? deletedAt = null)
    {
        return new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = fullName,
            Phone = "0900000001",
            AvatarUrl = $"https://cdn.example.com/{accountId}.png",
            Status = status,
            CreatedAt = createdAt,
            DeletedAt = deletedAt
        };
    }

    private static Project CreateProject(
        Guid customerId,
        Guid salesId,
        Guid designerId,
        ProjectStatus status,
        DateTime assignedAt)
    {
        return new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = customerId,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            ProjectCode = $"PRJ-{Guid.NewGuid():N}"[..12],
            ProjectName = $"Project {Guid.NewGuid():N}",
            FurnitureRequirement = "Tables",
            Status = status,
            DesignerAssignedAt = assignedAt,
            CreatedAt = assignedAt
        };
    }

    private sealed record SeededData(
        Guid CustomerId,
        Guid DesignerWithoutProjectsId,
        Guid DesignerPostDesignOnlyId,
        Guid DesignerWithOneProjectId,
        Guid DesignerAtCapacityId,
        Guid DesignerOverCapacityId,
        Guid DesignerRoleId,
        Guid SalesRoleId,
        Guid CustomerRoleId);
}
