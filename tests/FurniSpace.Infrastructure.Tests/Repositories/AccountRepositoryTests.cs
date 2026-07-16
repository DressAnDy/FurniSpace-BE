#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class AccountRepositoryTests
{
    [Fact]
    public async Task GetByEmailAndDetailAsync_ReturnAccountWithRoleProjection()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var account = await repository.GetByEmailAsync("customer@example.com");
        var detail = await repository.GetDetailAsync(data.CustomerId);

        Assert.NotNull(account);
        Assert.Equal(data.CustomerId, account.AccountId);
        Assert.NotNull(detail);
        Assert.Equal(data.CustomerId, detail.AccountId);
        Assert.Equal("customer@example.com", detail.Email);
        Assert.Equal("Customer User", detail.FullName);
        Assert.Equal("CUSTOMER", detail.Role.RoleName);
        Assert.Equal("Customer accounts", detail.Role.Description);
        Assert.Equal(AccountStatus.ACTIVE, detail.Status);
    }

    [Fact]
    public async Task RoleAndEmailLookups_ReturnExpectedValues()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var roleName = await repository.GetRoleNameAsync(data.DesignerRoleId);
        var roleId = await repository.GetRoleIdByNameAsync("DESIGNER");
        var roleExists = await repository.RoleExistsAsync(data.SalesRoleId);
        var duplicateEmail = await repository.EmailExistsAsync("customer@example.com");
        var excludedDuplicateEmail = await repository.EmailExistsAsync("customer@example.com", data.CustomerId);
        var missingEmail = await repository.EmailExistsAsync("missing@example.com");

        Assert.Equal("DESIGNER", roleName);
        Assert.Equal(data.DesignerRoleId, roleId);
        Assert.True(roleExists);
        Assert.True(duplicateEmail);
        Assert.False(excludedDuplicateEmail);
        Assert.False(missingEmail);
    }

    [Fact]
    public async Task GetPagedAndCountAsync_FilterByDeletedAndStatusAndOrderByCreatedAt()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new AccountRepository(context);

        var activeAccounts = await repository.GetPagedAsync(
            page: 1,
            pageSize: 10,
            search: null,
            status: "active",
            includeDeleted: false);
        var activeCount = await repository.CountAsync(
            search: null,
            status: "ACTIVE",
            includeDeleted: false);
        var allAccountsIncludingDeleted = await repository.GetPagedAsync(
            page: 1,
            pageSize: 20,
            search: null,
            status: "not-a-status",
            includeDeleted: true);

        Assert.Equal(4, activeCount);
        Assert.Equal(activeCount, activeAccounts.Count);
        Assert.DoesNotContain(activeAccounts, account => account.DeletedAt.HasValue);
        Assert.All(activeAccounts, account => Assert.Equal(AccountStatus.ACTIVE, account.Status));
        Assert.True(activeAccounts.SequenceEqual(
            activeAccounts
                .OrderByDescending(account => account.CreatedAt)
                .ThenBy(account => account.Email)));
        Assert.Equal(7, allAccountsIncludingDeleted.Count);
        Assert.Contains(allAccountsIncludingDeleted, account => account.DeletedAt.HasValue);
    }

    [Fact]
    public async Task FacetCounts_GroupByStatusAndRoleIdRespectDeletedFilter()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var statusCounts = await repository.CountGroupedByStatusAsync(includeDeleted: false);
        var roleCounts = await repository.CountGroupedByRoleIdAsync(includeDeleted: false);
        var statusCountsWithDeleted = await repository.CountGroupedByStatusAsync(includeDeleted: true);

        Assert.Contains(statusCounts, facet => facet.Key == "ACTIVE" && facet.Count == 4);
        Assert.Contains(statusCounts, facet => facet.Key == "INACTIVE" && facet.Count == 1);
        Assert.Contains(statusCounts, facet => facet.Key == "SUSPENDED" && facet.Count == 1);
        Assert.Contains(roleCounts, facet => facet.Key == data.DesignerRoleId.ToString() && facet.Count == 4);
        Assert.Contains(roleCounts, facet => facet.Key == data.CustomerRoleId.ToString() && facet.Count == 1);
        Assert.Contains(statusCountsWithDeleted, facet => facet.Key == "ACTIVE" && facet.Count == 5);
    }

    [Fact]
    public async Task GetAvailableDesignersAsync_ReturnsActiveDesignersBelowCapacityOrderedByLoad()
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

        Assert.Equal(2, count);
        Assert.Equal([data.DesignerWithoutProjectsId, data.DesignerWithOneProjectId], designers.Select(designer => designer.AccountId));
        Assert.Equal(0, designers[0].CurrentActiveProjectCount);
        Assert.Equal(2, designers[0].AvailableSlot);
        Assert.Equal(1, designers[1].CurrentActiveProjectCount);
        Assert.Equal(1, designers[1].AvailableSlot);
        Assert.All(designers, designer =>
        {
            Assert.Equal(AccountStatus.ACTIVE, designer.Status);
            Assert.Equal(2, designer.MaxActiveProjects);
        });
    }

    [Fact]
    public async Task GetAvailableDesignersAsync_IncludesDesignersAtCapacityWhenCapacityFilterDisabled()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new AccountRepository(context);

        var designers = await repository.GetAvailableDesignersAsync(
            page: 1,
            pageSize: 10,
            maxActiveProjects: 1,
            search: null);

        Assert.Equal(
            [data.DesignerWithoutProjectsId, data.DesignerWithOneProjectId],
            designers.Select(designer => designer.AccountId));
        Assert.Contains(designers, designer =>
            designer.AccountId == data.DesignerWithOneProjectId &&
            designer.CurrentActiveProjectCount == 1);
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
        var designerWithOneProjectId = Guid.NewGuid();
        var designerAtCapacityId = Guid.NewGuid();
        var inactiveDesignerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.RoleSet.AddRange(customerRole, salesRole, designerRole, adminRole);
        context.AccountSet.AddRange(
            CreateAccount(customerId, customerRole.RoleId, "customer@example.com", "Customer User", AccountStatus.ACTIVE, now.AddDays(-1)),
            CreateAccount(deletedCustomerId, customerRole.RoleId, "deleted@example.com", "Deleted User", AccountStatus.ACTIVE, now.AddDays(-2), deletedAt: now),
            CreateAccount(salesId, salesRole.RoleId, "sales@example.com", "Sales User", AccountStatus.ACTIVE, now.AddDays(-3)),
            CreateAccount(designerWithoutProjectsId, designerRole.RoleId, "amy.designer@example.com", "Amy Designer", AccountStatus.ACTIVE, now.AddDays(-4)),
            CreateAccount(designerWithOneProjectId, designerRole.RoleId, "beth.designer@example.com", "Beth Designer", AccountStatus.ACTIVE, now.AddDays(-5)),
            CreateAccount(designerAtCapacityId, designerRole.RoleId, "cara.designer@example.com", "Cara Designer", AccountStatus.SUSPENDED, now.AddDays(-6)),
            CreateAccount(inactiveDesignerId, designerRole.RoleId, "dana.designer@example.com", "Dana Designer", AccountStatus.INACTIVE, now.AddDays(-7)));

        context.ProjectSet.AddRange(
            CreateProject(designerWithOneProjectId, ProjectStatus.PROPOSAL_CONSULTING),
            CreateProject(designerAtCapacityId, ProjectStatus.IN_CONSULTATION),
            CreateProject(designerAtCapacityId, ProjectStatus.SPACE_VERIFIED),
            CreateProject(designerWithoutProjectsId, ProjectStatus.COMPLETED));

        await context.SaveChangesAsync();

        return new SeededData(
            CustomerId: customerId,
            DesignerWithoutProjectsId: designerWithoutProjectsId,
            DesignerWithOneProjectId: designerWithOneProjectId,
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

    private static Project CreateProject(Guid designerId, ProjectStatus status)
    {
        return new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            AssignedDesignerId = designerId,
            ProjectName = $"Project {Guid.NewGuid():N}",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed record SeededData(
        Guid CustomerId,
        Guid DesignerWithoutProjectsId,
        Guid DesignerWithOneProjectId,
        Guid DesignerRoleId,
        Guid SalesRoleId,
        Guid CustomerRoleId);
}
