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

public sealed class ProjectWorkflowRepositoryTests
{
    [Fact]
    public async Task GetSnapshotAsync_MissingProject_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new ProjectWorkflowRepository(context);

        var snapshot = await repository.GetSnapshotAsync(Guid.NewGuid());

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task GetSnapshotAsync_ProjectWithoutChildren_ReturnsNamesAndEmptyCollections()
    {
        await using var context = CreateContext();
        var seed = await SeedMinimalProjectAsync(context);
        var repository = new ProjectWorkflowRepository(context);

        var snapshot = await repository.GetSnapshotAsync(seed.ProjectId);

        Assert.NotNull(snapshot);
        Assert.Equal(seed.ProjectId, snapshot.ProjectId);
        Assert.Equal("PRJ-WF", snapshot.ProjectCode);
        Assert.Equal("Workflow Project", snapshot.ProjectName);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, snapshot.Status);
        Assert.Equal("Cafe", snapshot.BusinessType);
        Assert.Equal(seed.CustomerId, snapshot.CustomerId);
        Assert.Equal("Customer WF", snapshot.CustomerName);
        Assert.Equal(seed.SalesId, snapshot.AssignedSalesId);
        Assert.Equal("Sales WF", snapshot.SalesName);
        Assert.Equal(seed.DesignerId, snapshot.AssignedDesignerId);
        Assert.Equal("Designer WF", snapshot.DesignerName);
        Assert.Empty(snapshot.Proposals);
        Assert.Empty(snapshot.Quotations);
        Assert.Empty(snapshot.Orders);
        Assert.Empty(snapshot.OrderItems);
        Assert.Empty(snapshot.ProductionRequests);
        Assert.Empty(snapshot.ProductionItems);
        Assert.Empty(snapshot.Schedules);
        Assert.Empty(snapshot.Payments);
    }

    [Fact]
    public async Task GetSnapshotAsync_FullGraph_MapsRelatedReadModelsAndAssigneeNames()
    {
        await using var context = CreateContext();
        var seed = await SeedFullWorkflowAsync(context);
        var repository = new ProjectWorkflowRepository(context);

        var snapshot = await repository.GetSnapshotAsync(seed.ProjectId);

        Assert.NotNull(snapshot);
        Assert.Single(snapshot.Proposals);
        Assert.Equal(seed.ProposalId, snapshot.Proposals[0].ProposalId);
        Assert.Equal(ProposalStatus.SELECTED, snapshot.Proposals[0].Status);

        Assert.Single(snapshot.Quotations);
        Assert.Equal(seed.QuotationId, snapshot.Quotations[0].QuotationId);
        Assert.Equal(1200m, snapshot.Quotations[0].TotalAmount);

        Assert.Single(snapshot.Orders);
        Assert.Equal(seed.OrderId, snapshot.Orders[0].OrderId);
        Assert.Equal(700m, snapshot.Orders[0].RemainingAmount);

        Assert.Single(snapshot.OrderItems);
        Assert.Equal(OrderItemStatus.DELIVERED, snapshot.OrderItems[0].Status);
        Assert.NotNull(snapshot.OrderItems[0].DeliveredAt);

        Assert.Single(snapshot.ProductionRequests);
        Assert.Equal(seed.ProductionRequestId, snapshot.ProductionRequests[0].ProductionRequestId);
        Assert.Equal(seed.ProductionId, snapshot.ProductionRequests[0].AssignedTo);
        Assert.Equal("Production WF", snapshot.ProductionRequests[0].AssignedToName);

        Assert.Single(snapshot.ProductionItems);
        Assert.Equal(ProductionItemStatus.IN_PRODUCTION, snapshot.ProductionItems[0].Status);

        Assert.Equal(2, snapshot.Schedules.Count);
        Assert.Contains(snapshot.Schedules, s => s.ScheduleType == ProjectScheduleType.MEASUREMENT);
        Assert.Contains(snapshot.Schedules, s => s.ScheduleType == ProjectScheduleType.DELIVERY);

        Assert.Single(snapshot.Payments);
        Assert.Equal(seed.PaymentId, snapshot.Payments[0].PaymentId);
        Assert.Equal(PaymentType.DEPOSIT, snapshot.Payments[0].PaymentType);
    }

    [Fact]
    public void ReadModels_PropertyCoverage()
    {
        var now = DateTime.UtcNow;
        var proposalId = Guid.NewGuid();
        var snapshot = new FurniSpace.Infrastructure.ReadModels.Projects.ProjectWorkflowSnapshotReadModel
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "C",
            ProjectName = "N",
            Status = ProjectStatus.DELIVERING,
            BusinessType = "B",
            SubmittedAt = now,
            SalesAssignedAt = now,
            DesignerAssignedAt = now,
            RejectedAt = null,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Cust",
            AssignedSalesId = Guid.NewGuid(),
            SalesName = "S",
            AssignedDesignerId = Guid.NewGuid(),
            DesignerName = "D",
            Proposals =
            [
                new()
                {
                    ProposalId = proposalId,
                    ProposalName = "P",
                    Status = ProposalStatus.DRAFT,
                    VersionNo = 1,
                    UpdatedAt = now,
                    SelectedAt = null
                }
            ],
            Quotations =
            [
                new()
                {
                    QuotationId = Guid.NewGuid(),
                    QuotationCode = "Q",
                    Status = QuotationStatus.SENT,
                    TotalAmount = 1m,
                    SentAt = now,
                    UpdatedAt = now
                }
            ],
            Orders =
            [
                new()
                {
                    OrderId = Guid.NewGuid(),
                    OrderCode = "O",
                    Status = OrderStatus.DELIVERING,
                    RemainingAmount = 2m,
                    CreatedAt = now
                }
            ],
            OrderItems =
            [
                new()
                {
                    OrderId = Guid.NewGuid(),
                    Quantity = 3,
                    Status = OrderItemStatus.READY,
                    DeliveredAt = null
                }
            ],
            ProductionRequests =
            [
                new()
                {
                    ProductionRequestId = Guid.NewGuid(),
                    ProductionCode = "PR",
                    Status = ProductionRequestStatus.IN_PRODUCTION,
                    EstimatedCompletionDate = DateOnly.FromDateTime(now),
                    AssignedTo = Guid.NewGuid(),
                    AssignedToName = "Prod",
                    CreatedAt = now
                }
            ],
            ProductionItems =
            [
                new()
                {
                    ProductionRequestId = Guid.NewGuid(),
                    Status = ProductionItemStatus.PENDING,
                    EstimatedCompletionDate = DateOnly.FromDateTime(now)
                }
            ],
            Schedules =
            [
                new()
                {
                    ScheduleId = Guid.NewGuid(),
                    Title = "T",
                    ScheduleType = ProjectScheduleType.HANDOVER,
                    Status = ProjectScheduleStatus.CONFIRMED,
                    ScheduledStart = now,
                    ScheduledEnd = now.AddHours(1)
                }
            ],
            Payments =
            [
                new()
                {
                    PaymentId = Guid.NewGuid(),
                    PaymentCode = "PAY",
                    PaymentType = PaymentType.REMAINING_PAYMENT,
                    Status = PaymentStatus.PENDING,
                    CreatedAt = now
                }
            ]
        };

        Assert.Equal(proposalId, snapshot.Proposals[0].ProposalId);
        Assert.Equal("Q", snapshot.Quotations[0].QuotationCode);
        Assert.Equal("O", snapshot.Orders[0].OrderCode);
        Assert.Equal(3, snapshot.OrderItems[0].Quantity);
        Assert.Equal("Prod", snapshot.ProductionRequests[0].AssignedToName);
        Assert.Equal(ProductionItemStatus.PENDING, snapshot.ProductionItems[0].Status);
        Assert.Equal(ProjectScheduleType.HANDOVER, snapshot.Schedules[0].ScheduleType);
        Assert.Equal(PaymentType.REMAINING_PAYMENT, snapshot.Payments[0].PaymentType);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<MinimalSeed> SeedMinimalProjectAsync(AppDbContext context)
    {
        var now = new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
        var customerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER" };
        var salesRole = new Role { RoleId = Guid.NewGuid(), RoleName = "SALES" };
        var designerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "DESIGNER" };

        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        context.RoleSet.AddRange(customerRole, salesRole, designerRole);
        context.AccountSet.AddRange(
            CreateAccount(customerId, customerRole.RoleId, "customer-wf@example.com", "Customer WF"),
            CreateAccount(salesId, salesRole.RoleId, "sales-wf@example.com", "Sales WF"),
            CreateAccount(designerId, designerRole.RoleId, "designer-wf@example.com", "Designer WF"));

        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            ProjectCode = "PRJ-WF",
            ProjectName = "Workflow Project",
            BusinessType = "Cafe",
            Status = ProjectStatus.IN_CONSULTATION,
            SubmittedAt = now.AddDays(-2),
            SalesAssignedAt = now.AddDays(-1),
            DesignerAssignedAt = now,
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
        return new MinimalSeed(projectId, customerId, salesId, designerId);
    }

    private static async Task<FullSeed> SeedFullWorkflowAsync(AppDbContext context)
    {
        var now = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
        var customerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER" };
        var salesRole = new Role { RoleId = Guid.NewGuid(), RoleName = "SALES" };
        var designerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "DESIGNER" };
        var productionRole = new Role { RoleId = Guid.NewGuid(), RoleName = "PRODUCTION" };

        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var productionRequestId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        context.RoleSet.AddRange(customerRole, salesRole, designerRole, productionRole);
        context.AccountSet.AddRange(
            CreateAccount(customerId, customerRole.RoleId, "customer-full@example.com", "Customer Full"),
            CreateAccount(salesId, salesRole.RoleId, "sales-full@example.com", "Sales Full"),
            CreateAccount(designerId, designerRole.RoleId, "designer-full@example.com", "Designer Full"),
            CreateAccount(productionId, productionRole.RoleId, "production-wf@example.com", "Production WF"));

        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            ProjectCode = "PRJ-FULL",
            ProjectName = "Full Workflow",
            BusinessType = "Office",
            Status = ProjectStatus.DELIVERING,
            SubmittedAt = now.AddDays(-20),
            SalesAssignedAt = now.AddDays(-19),
            DesignerAssignedAt = now.AddDays(-18),
            CreatedAt = now.AddDays(-21),
            UpdatedAt = now
        });

        context.ProposalSet.Add(new Proposal
        {
            ProposalId = proposalId,
            ProjectId = projectId,
            ProposalName = "Selected proposal",
            Status = ProposalStatus.SELECTED,
            VersionNo = 2,
            SelectedAt = now.AddDays(-10),
            UpdatedAt = now.AddDays(-10),
            CreatedAt = now.AddDays(-12)
        });

        context.QuotationSet.Add(new Quotation
        {
            QuotationId = quotationId,
            ProjectId = projectId,
            ProposalId = proposalId,
            QuotationCode = "QUO-WF",
            VersionNo = 1,
            SubtotalAmount = 1111.11m,
            TotalDiscountAmount = 0m,
            PreVatAmount = 1111.11m,
            VatRate = 0.08m,
            VatAmount = 88.89m,
            TotalAmount = 1200m,
            DepositAmount = 500m,
            Currency = "VND",
            Status = QuotationStatus.ACCEPTED,
            SentAt = now.AddDays(-9),
            UpdatedAt = now.AddDays(-8),
            CreatedAt = now.AddDays(-9)
        });

        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = quotationId,
            OrderCode = "ORD-WF",
            CustomerId = customerId,
            SalesId = salesId,
            FinalTotalAmount = 1200m,
            PaidAmount = 500m,
            RemainingAmount = 700m,
            Status = OrderStatus.DELIVERING,
            CreatedAt = now.AddDays(-7),
            UpdatedAt = now
        });

        context.OrderItemSet.Add(new OrderItem
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            ProductNameSnapshot = "Table",
            Quantity = 2,
            Status = OrderItemStatus.DELIVERED,
            DeliveredAt = now.AddHours(-3),
            UnitPrice = 600m,
            DiscountAmount = 0m,
            SubtotalAmount = 1200m
        });

        context.ProductionRequestSet.Add(new ProductionRequest
        {
            ProductionRequestId = productionRequestId,
            ProductionCode = "PR-WF",
            ProjectId = projectId,
            OrderId = orderId,
            AssignedTo = productionId,
            Status = ProductionRequestStatus.IN_PRODUCTION,
            EstimatedCompletionDate = DateOnly.FromDateTime(now.AddDays(2)),
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now
        });

        context.ProductionItemSet.Add(new ProductionItem
        {
            ProductionItemId = Guid.NewGuid(),
            ProductionRequestId = productionRequestId,
            OrderItemId = orderItemId,
            Status = ProductionItemStatus.IN_PRODUCTION,
            ProductNameSnapshot = "Table",
            Quantity = 2,
            EstimatedCompletionDate = DateOnly.FromDateTime(now.AddDays(1))
        });

        context.ProjectScheduleSet.AddRange(
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Measure",
                Status = ProjectScheduleStatus.COMPLETED,
                ScheduledStart = now.AddDays(-15),
                ScheduledEnd = now.AddDays(-15).AddHours(2),
                CreatedAt = now.AddDays(-16)
            },
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Title = "Deliver",
                Status = ProjectScheduleStatus.CONFIRMED,
                ScheduledStart = now.AddDays(1),
                ScheduledEnd = now.AddDays(1).AddHours(3),
                CreatedAt = now
            });

        context.PaymentSet.Add(new Payment
        {
            PaymentId = paymentId,
            ProjectId = projectId,
            OrderId = orderId,
            QuotationId = quotationId,
            PaymentCode = "PAY-WF",
            PaymentType = PaymentType.DEPOSIT,
            Amount = 500m,
            Status = PaymentStatus.PAID,
            PaidAt = now.AddDays(-6),
            CreatedAt = now.AddDays(-6),
            UpdatedAt = now.AddDays(-6)
        });

        await context.SaveChangesAsync();

        return new FullSeed(
            projectId,
            proposalId,
            quotationId,
            orderId,
            productionRequestId,
            paymentId,
            productionId);
    }

    private static Account CreateAccount(Guid accountId, Guid roleId, string email, string fullName) =>
        new()
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = fullName,
            Status = AccountStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };

    private sealed record MinimalSeed(Guid ProjectId, Guid CustomerId, Guid SalesId, Guid DesignerId);

    private sealed record FullSeed(
        Guid ProjectId,
        Guid ProposalId,
        Guid QuotationId,
        Guid OrderId,
        Guid ProductionRequestId,
        Guid PaymentId,
        Guid ProductionId);
}
