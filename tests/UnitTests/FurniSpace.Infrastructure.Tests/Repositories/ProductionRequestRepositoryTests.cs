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

public sealed class ProductionRequestRepositoryTests
{
    [Fact]
    public async Task GetAvailableStaffAsync_InMemorySearch_ReturnsActiveProductionStaffWithWorkload()
    {
        await using var context = CreateContext();
        var productionRole = new Role { RoleId = Guid.NewGuid(), RoleName = "PRODUCTION" };
        var salesRole = new Role { RoleId = Guid.NewGuid(), RoleName = "SALES" };
        var staffId = Guid.NewGuid();
        var deletedStaffId = Guid.NewGuid();
        var inactiveStaffId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        context.RoleSet.AddRange(productionRole, salesRole);
        context.AccountSet.AddRange(
            CreateAccount(staffId, productionRole.RoleId, "maker@example.com", AccountStatus.ACTIVE, "Demo Production"),
            CreateAccount(deletedStaffId, productionRole.RoleId, "deleted@example.com", AccountStatus.ACTIVE, "Deleted Production", DateTime.UtcNow),
            CreateAccount(inactiveStaffId, productionRole.RoleId, "inactive@example.com", AccountStatus.INACTIVE, "Inactive Production"),
            CreateAccount(salesId, salesRole.RoleId, "sales@example.com", AccountStatus.ACTIVE, "Sales"));
        context.ProjectSet.Add(CreateProject(projectId, salesId));
        context.OrderSet.Add(CreateOrder(orderId, projectId, salesId));
        context.ProductionRequestSet.AddRange(
            CreateRequest(orderId, projectId, staffId, ProductionRequestStatus.PENDING_REVIEW),
            CreateRequest(orderId, projectId, staffId, ProductionRequestStatus.FEASIBLE),
            CreateRequest(orderId, projectId, staffId, ProductionRequestStatus.COMPLETED),
            CreateRequest(orderId, projectId, deletedStaffId, ProductionRequestStatus.IN_PRODUCTION),
            CreateRequest(orderId, projectId, inactiveStaffId, ProductionRequestStatus.IN_PRODUCTION));
        await context.SaveChangesAsync();
        var repository = new ProductionRequestRepository(context);

        var staff = await repository.GetAvailableStaffAsync("production");

        var item = Assert.Single(staff);
        Assert.Equal(staffId, item.AccountId);
        Assert.Equal(2, item.ActiveRequestCount);
        Assert.Equal(1, item.PendingReviewRequestCount);
        Assert.Equal(0, item.InProductionRequestCount);
        Assert.Equal(0, item.BlockedRequestCount);
        Assert.Equal(AccountStatus.ACTIVE, item.AccountStatus);
        Assert.DoesNotContain(staff, item => item.AccountId == deletedStaffId);
        Assert.DoesNotContain(staff, item => item.AccountId == inactiveStaffId);
    }

    [Fact]
    public async Task GetAvailableStaffAsync_InMemorySearchByEmail_IsCaseInsensitive()
    {
        await using var context = CreateContext();
        var role = new Role { RoleId = Guid.NewGuid(), RoleName = "PRODUCTION" };
        var staffId = Guid.NewGuid();
        context.RoleSet.Add(role);
        context.AccountSet.Add(CreateAccount(staffId, role.RoleId, "Maker@Example.com", AccountStatus.ACTIVE, "Maker"));
        await context.SaveChangesAsync();
        var repository = new ProductionRequestRepository(context);

        var staff = await repository.GetAvailableStaffAsync("EXAMPLE");

        Assert.Equal(staffId, Assert.Single(staff).AccountId);
    }

    [Theory]
    [InlineData(ProductionRequestStatus.PENDING_REVIEW, true)]
    [InlineData(ProductionRequestStatus.FEASIBLE, true)]
    [InlineData(ProductionRequestStatus.IN_PRODUCTION, true)]
    [InlineData(ProductionRequestStatus.COMPLETED, true)]
    [InlineData(ProductionRequestStatus.CANCELLED, false)]
    public async Task HasViewableAssignedRequestAsync_UsesScheduleReadStatusPolicy(
        ProductionRequestStatus status,
        bool expected)
    {
        await using var context = CreateContext();
        var staffId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        context.ProjectSet.AddRange(CreateProject(projectId, salesId), CreateProject(otherProjectId, salesId));
        context.OrderSet.Add(CreateOrder(orderId, projectId, salesId));
        context.ProductionRequestSet.AddRange(
            CreateRequest(orderId, projectId, staffId, status),
            CreateRequest(orderId, otherProjectId, staffId, ProductionRequestStatus.IN_PRODUCTION));
        await context.SaveChangesAsync();
        var repository = new ProductionRequestRepository(context);

        var hasAccess = await repository.HasViewableAssignedRequestAsync(projectId, staffId);
        var hasOtherStaffAccess = await repository.HasViewableAssignedRequestAsync(projectId, Guid.NewGuid());

        Assert.Equal(expected, hasAccess);
        Assert.False(hasOtherStaffAccess);
    }

    [Fact]
    public async Task HasViewableAssignedRequestAsync_ReturnsFalse_ForDifferentProject()
    {
        await using var context = CreateContext();
        var staffId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        context.ProjectSet.AddRange(CreateProject(projectId, salesId), CreateProject(otherProjectId, salesId));
        context.OrderSet.Add(CreateOrder(orderId, otherProjectId, salesId));
        context.ProductionRequestSet.Add(CreateRequest(
            orderId,
            otherProjectId,
            staffId,
            ProductionRequestStatus.IN_PRODUCTION));
        await context.SaveChangesAsync();
        var repository = new ProductionRequestRepository(context);

        var hasAccess = await repository.HasViewableAssignedRequestAsync(projectId, staffId);

        Assert.False(hasAccess);
    }

    [Fact]
    public async Task GetMaxOperationalProductionDateAsync_ReturnsLatestOperationalDate()
    {
        await using var context = CreateContext();
        var productionRole = new Role { RoleId = Guid.NewGuid(), RoleName = "PRODUCTION" };
        var staffId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        context.RoleSet.Add(productionRole);
        context.AccountSet.Add(CreateAccount(staffId, productionRole.RoleId, "maker@example.com", AccountStatus.ACTIVE, "Maker"));
        context.ProjectSet.Add(CreateProject(projectId, salesId));
        context.OrderSet.Add(CreateOrder(orderId, projectId, salesId));
        context.ProductionRequestSet.AddRange(
            CreateRequest(orderId, projectId, staffId, ProductionRequestStatus.IN_PRODUCTION),
            new ProductionRequest
            {
                ProductionRequestId = Guid.NewGuid(),
                OrderId = orderId,
                ProjectId = projectId,
                ProductionCode = "PRD-LATER",
                AssignedTo = staffId,
                Status = ProductionRequestStatus.COMPLETED,
                EstimatedCompletionDate = new DateOnly(2026, 12, 31),
                ActualCompletionDate = new DateOnly(2026, 11, 15),
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        var repository = new ProductionRequestRepository(context);

        var maxDate = await repository.GetMaxOperationalProductionDateAsync(projectId);

        Assert.Equal(new DateOnly(2026, 12, 31), maxDate);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Account CreateAccount(
        Guid accountId,
        Guid roleId,
        string email,
        AccountStatus status,
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
            Status = status,
            DeletedAt = deletedAt
        };
    }

    private static Project CreateProject(Guid projectId, Guid salesId)
    {
        return new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectCode = "PRJ-PROD",
            ProjectName = "Production Project",
            BusinessType = "Cafe",
            Status = ProjectStatus.IN_PRODUCTION,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Order CreateOrder(Guid orderId, Guid projectId, Guid salesId)
    {
        return new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-PROD",
            CustomerId = Guid.NewGuid(),
            SalesId = salesId,
            Status = OrderStatus.IN_PRODUCTION,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static ProductionRequest CreateRequest(
        Guid orderId,
        Guid projectId,
        Guid staffId,
        ProductionRequestStatus status)
    {
        return new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            OrderId = orderId,
            ProjectId = projectId,
            ProductionCode = $"PRD-{Guid.NewGuid():N}"[..12],
            AssignedTo = staffId,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }
}
