#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class ProjectRepositoryTests
{
    [Fact]
    public async Task GetByUserAsync_WithCustomerScope_ReturnsCustomerProjectsWithAccountSummaries()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectRepository(context);

        var projects = await repository.GetByUserAsync(new ProjectByUserQueryReadModel
        {
            UserId = data.CustomerId,
            RoleScope = "CUSTOMER",
            Page = 1,
            PageSize = 10
        });
        var count = await repository.CountByUserAsync(new ProjectByUserQueryReadModel
        {
            UserId = data.CustomerId,
            RoleScope = "CUSTOMER",
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(2, count);
        Assert.Equal(2, projects.Count);
        Assert.All(projects, project => Assert.Equal(data.CustomerId, project.Customer.AccountId));
        Assert.Contains(projects, project => project.AssignedSales?.AccountId == data.SalesId);
        Assert.Contains(projects, project => project.AssignedDesigner?.AccountId == data.DesignerId);
    }

    [Fact]
    public async Task GetByUserAsync_WithSalesDesignerAndAdminScopes_FiltersByRelationship()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectRepository(context);

        var salesProjects = await repository.GetByUserAsync(new ProjectByUserQueryReadModel
        {
            UserId = data.SalesId,
            RoleScope = "SALES",
            Page = 1,
            PageSize = 10
        });
        var designerProjects = await repository.GetByUserAsync(new ProjectByUserQueryReadModel
        {
            UserId = data.DesignerId,
            RoleScope = "DESIGNER",
            Status = ProjectStatus.PROPOSAL_CONSULTING,
            Page = 1,
            PageSize = 10
        });
        var adminCount = await repository.CountByUserAsync(new ProjectByUserQueryReadModel
        {
            UserId = data.AdminId,
            RoleScope = "ADMIN",
            Page = 1,
            PageSize = 10
        });

        Assert.Single(salesProjects);
        Assert.Equal(data.SalesId, salesProjects[0].AssignedSales?.AccountId);
        Assert.Single(designerProjects);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, designerProjects[0].Status);
        Assert.Equal(3, adminCount);
    }

    [Fact]
    public async Task GetAccountRoleNameAsync_ExcludesDeletedAccount()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectRepository(context);

        var activeRole = await repository.GetAccountRoleNameAsync(data.CustomerId);
        var deletedRole = await repository.GetAccountRoleNameAsync(data.DeletedCustomerId);

        Assert.Equal("CUSTOMER", activeRole);
        Assert.Null(deletedRole);
    }

    [Fact]
    public async Task GetListAsync_WithAssignmentFilters_ReturnsQueueItems()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectRepository(context);

        var projects = await repository.GetListAsync(new ProjectListQueryReadModel
        {
            AssignedDesignerId = data.DesignerId,
            Status = ProjectStatus.PROPOSAL_CONSULTING,
            Page = 1,
            Limit = 10
        });
        var count = await repository.CountAsync(new ProjectListQueryReadModel
        {
            AssignedDesignerId = data.DesignerId,
            Status = ProjectStatus.PROPOSAL_CONSULTING,
            Page = 1,
            Limit = 10
        });

        Assert.Equal(1, count);
        Assert.Single(projects);
        Assert.Equal(data.DesignerId, projects[0].AssignedDesignerId);
    }

    [Fact]
    public async Task GetDetailAndSearchIndexQueries_ReturnProjectReadModels()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectRepository(context);

        var detail = await repository.GetDetailAsync(data.PrimaryProjectId);
        var searchItem = await repository.GetSearchIndexItemAsync(data.PrimaryProjectId);
        var searchPage = await repository.GetSearchIndexPageAsync(page: 1, limit: 10);

        Assert.NotNull(detail);
        Assert.Equal(data.PrimaryProjectId, detail.ProjectId);
        Assert.Equal("Luxury Cafe Interior", detail.ProjectName);
        Assert.NotNull(searchItem);
        Assert.Equal(data.PrimaryProjectId, searchItem.ProjectId);
        Assert.Equal("Michael Chen", searchItem.CustomerName);
        Assert.Equal("michael@example.com", searchItem.CustomerEmail);
        Assert.Contains(searchPage, project => project.ProjectId == data.PrimaryProjectId);
    }

    [Fact]
    public async Task AccountHelpers_ReturnActiveNamesAndDesigners()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectRepository(context);

        var fullName = await repository.GetAccountFullNameAsync(data.CustomerId);
        var deletedFullName = await repository.GetAccountFullNameAsync(data.DeletedCustomerId);
        var activeDesigner = await repository.GetActiveDesignerAsync(data.DesignerId);
        var nonDesigner = await repository.GetActiveDesignerAsync(data.SalesId);
        var activeIds = await repository.GetActiveAccountIdsByRoleNamesAsync([" designer ", "DESIGNER", " ", "sales"]);
        var emptyIds = await repository.GetActiveAccountIdsByRoleNamesAsync([" ", ""]);

        Assert.Equal("Michael Chen", fullName);
        Assert.Null(deletedFullName);
        Assert.NotNull(activeDesigner);
        Assert.Equal(data.DesignerId, activeDesigner.AccountId);
        Assert.Null(nonDesigner);
        Assert.Contains(data.DesignerId, activeIds);
        Assert.Contains(data.SalesId, activeIds);
        Assert.Empty(emptyIds);
    }

    [Fact]
    public async Task CountSubmittedInYear_UsesSubmittedAtOrCreatedAt()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectRepository(context);

        var submittedCount = await repository.CountSubmittedInYearAsync(2026);
        var missingCount = await repository.CountSubmittedInYearAsync(2025);

        Assert.Equal(3, submittedCount);
        Assert.Equal(0, missingCount);
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
        var customerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER" };
        var salesRole = new Role { RoleId = Guid.NewGuid(), RoleName = "SALES" };
        var designerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "DESIGNER" };
        var adminRole = new Role { RoleId = Guid.NewGuid(), RoleName = "ADMIN" };
        var customerId = Guid.NewGuid();
        var deletedCustomerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        context.RoleSet.AddRange(customerRole, salesRole, designerRole, adminRole);
        context.AccountSet.AddRange(
            CreateAccount(customerId, customerRole.RoleId, "michael@example.com", "Michael Chen"),
            CreateAccount(deletedCustomerId, customerRole.RoleId, "deleted@example.com", "Deleted Customer", DateTime.UtcNow),
            CreateAccount(salesId, salesRole.RoleId, "sales@example.com", "Sarah Johnson"),
            CreateAccount(designerId, designerRole.RoleId, "designer@example.com", "Emily Davis"),
            CreateAccount(adminId, adminRole.RoleId, "admin@example.com", "Admin User"));
        var primaryProjectId = Guid.NewGuid();
        context.ProjectSet.AddRange(
            new Project
            {
                ProjectId = primaryProjectId,
                CustomerId = customerId,
                AssignedSalesId = salesId,
                AssignedDesignerId = designerId,
                ProjectCode = "PRJ-2026-0001",
                ProjectName = "Luxury Cafe Interior",
                BusinessType = "Cafe",
                ProjectAddress = "123 Main Street",
                Status = ProjectStatus.PROPOSAL_CONSULTING,
                SubmittedAt = new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 6, 9, 8, 0, 0, DateTimeKind.Utc)
            },
            new Project
            {
                ProjectId = Guid.NewGuid(),
                CustomerId = customerId,
                ProjectCode = "PRJ-2026-0002",
                ProjectName = "Retail Counter",
                BusinessType = "Retail",
                Status = ProjectStatus.SUBMITTED,
                SubmittedAt = new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc)
            },
            new Project
            {
                ProjectId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                ProjectCode = "PRJ-2026-0003",
                ProjectName = "Other Project",
                BusinessType = "Office",
                Status = ProjectStatus.IN_CONSULTATION,
                SubmittedAt = null,
                CreatedAt = new DateTime(2026, 6, 6, 8, 0, 0, DateTimeKind.Utc)
            });

        await context.SaveChangesAsync();
        return new SeededData(customerId, deletedCustomerId, salesId, designerId, adminId, primaryProjectId);
    }

    private static Account CreateAccount(
        Guid accountId,
        Guid roleId,
        string email,
        string fullName,
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
            Status = AccountStatus.ACTIVE,
            DeletedAt = deletedAt
        };
    }

    private sealed record SeededData(
        Guid CustomerId,
        Guid DeletedCustomerId,
        Guid SalesId,
        Guid DesignerId,
        Guid AdminId,
        Guid PrimaryProjectId);
}
