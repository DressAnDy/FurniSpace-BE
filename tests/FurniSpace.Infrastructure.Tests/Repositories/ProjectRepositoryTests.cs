#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.DTOs.Projects;
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
            Status = ProjectStatus.PROPOSAL_DRAFTING,
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
        Assert.Equal(ProjectStatus.PROPOSAL_DRAFTING, designerProjects[0].Status);
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
            Status = ProjectStatus.PROPOSAL_DRAFTING,
            Page = 1,
            Limit = 10
        });
        var count = await repository.CountAsync(new ProjectListQueryReadModel
        {
            AssignedDesignerId = data.DesignerId,
            Status = ProjectStatus.PROPOSAL_DRAFTING,
            Page = 1,
            Limit = 10
        });

        Assert.Equal(1, count);
        Assert.Single(projects);
        Assert.Equal(data.DesignerId, projects[0].AssignedDesignerId);
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
        context.ProjectSet.AddRange(
            new Project
            {
                ProjectId = Guid.NewGuid(),
                CustomerId = customerId,
                AssignedSalesId = salesId,
                AssignedDesignerId = designerId,
                ProjectCode = "PRJ-2026-0001",
                ProjectName = "Luxury Cafe Interior",
                BusinessType = "Cafe",
                ProjectAddress = "123 Main Street",
                Status = ProjectStatus.PROPOSAL_DRAFTING,
                SubmittedAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Project
            {
                ProjectId = Guid.NewGuid(),
                CustomerId = customerId,
                ProjectCode = "PRJ-2026-0002",
                ProjectName = "Retail Counter",
                BusinessType = "Retail",
                Status = ProjectStatus.SUBMITTED,
                SubmittedAt = DateTime.UtcNow.AddDays(-3),
                CreatedAt = DateTime.UtcNow.AddDays(-4)
            },
            new Project
            {
                ProjectId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                ProjectCode = "PRJ-2026-0003",
                ProjectName = "Other Project",
                BusinessType = "Office",
                Status = ProjectStatus.IN_CONSULTATION,
                SubmittedAt = DateTime.UtcNow.AddDays(-5),
                CreatedAt = DateTime.UtcNow.AddDays(-6)
            });

        await context.SaveChangesAsync();
        return new SeededData(customerId, deletedCustomerId, salesId, designerId, adminId);
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
        Guid AdminId);
}
