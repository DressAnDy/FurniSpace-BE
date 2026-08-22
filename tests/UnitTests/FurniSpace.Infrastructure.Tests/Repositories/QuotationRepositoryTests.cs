using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Quotations;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class QuotationRepositoryTests
{
    [Fact]
    public async Task GetByProjectAsync_FiltersByProjectAndStatus()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        SeedProject(context, projectId);
        SeedProject(context, Guid.NewGuid());
        context.QuotationSet.Add(MakeQuotation(projectId, QuotationStatus.SENT, versionNo: 1));
        context.QuotationSet.Add(MakeQuotation(projectId, QuotationStatus.DRAFT, versionNo: 2));
        context.QuotationSet.Add(MakeQuotation(Guid.NewGuid(), QuotationStatus.SENT, versionNo: 3));
        await context.SaveChangesAsync();
        var repository = new QuotationRepository(context);

        var result = await repository.GetByProjectAsync(new QuotationQueryReadModel
        {
            ProjectId = projectId,
            Status = QuotationStatus.SENT
        });

        var item = Assert.Single(result);
        Assert.Equal(projectId, item.ProjectId);
        Assert.Equal(QuotationStatus.SENT, item.Status);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsQuotationWithItemsAndAssignments()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var quotation = MakeQuotation(projectId, QuotationStatus.SENT, versionNo: 1);
        SeedProject(context, projectId);
        context.QuotationSet.Add(quotation);
        context.QuotationItemSet.Add(new QuotationItem
        {
            QuotationItemId = Guid.NewGuid(),
            QuotationId = quotation.QuotationId,
            ItemName = "Delivery fee",
            Quantity = 1,
            UnitPrice = 50m,
            GrossAmount = 50m,
            DiscountAmount = 0m,
            TotalAmount = 50m
        });
        await context.SaveChangesAsync();
        var repository = new QuotationRepository(context);

        var result = await repository.GetDetailAsync(quotation.QuotationId);

        Assert.NotNull(result);
        Assert.Equal(projectId, result.ProjectId);
        Assert.NotEqual(Guid.Empty, result.CustomerId);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsDepositAmount()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var quotation = MakeQuotation(projectId, QuotationStatus.DRAFT, versionNo: 1);
        quotation.TotalAmount = 1_000m;
        quotation.DepositAmount = 300m;
        SeedProject(context, projectId);
        context.QuotationSet.Add(quotation);
        await context.SaveChangesAsync();
        var repository = new QuotationRepository(context);

        var result = await repository.GetDetailAsync(quotation.QuotationId);

        Assert.NotNull(result);
        Assert.Equal(300m, result!.DepositAmount);
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsDepositAmount()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var quotation = MakeQuotation(projectId, QuotationStatus.DRAFT, versionNo: 1);
        quotation.TotalAmount = 500m;
        quotation.DepositAmount = 150m;
        SeedProject(context, projectId);
        context.QuotationSet.Add(quotation);
        await context.SaveChangesAsync();
        var repository = new QuotationRepository(context);

        var result = await repository.GetByProjectAsync(new QuotationQueryReadModel { ProjectId = projectId });

        var item = Assert.Single(result);
        Assert.Equal(150m, item.DepositAmount);
    }

    [Fact]
    public async Task GetSelectedProposalAsync_ReturnsOnlySelectedProposal()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var selectedProposalId = Guid.NewGuid();
        SeedProject(context, projectId, ProjectStatus.PROPOSAL_SELECTED);
        context.ProposalSet.AddRange(
            MakeProposal(projectId, Guid.NewGuid(), ProposalStatus.PUBLISHED),
            MakeProposal(projectId, selectedProposalId, ProposalStatus.SELECTED));
        await context.SaveChangesAsync();
        var repository = new QuotationRepository(context);

        var result = await repository.GetSelectedProposalAsync(projectId);

        Assert.NotNull(result);
        Assert.Equal(selectedProposalId, result.ProposalId);
        Assert.Equal(ProposalStatus.SELECTED, result.ProposalStatus);
    }

    [Fact]
    public async Task HasQuotationForProposalAsync_IgnoresCancelledQuotation()
    {
        await using var context = CreateContext();
        var proposalId = Guid.NewGuid();
        context.QuotationSet.Add(MakeQuotation(Guid.NewGuid(), QuotationStatus.CANCELLED, proposalId));
        await context.SaveChangesAsync();
        var repository = new QuotationRepository(context);

        var result = await repository.HasQuotationForProposalAsync(proposalId);

        Assert.False(result);
    }

    [Fact]
    public async Task GetProposalItemsAsync_ReturnsItemsOrderedByCreatedAt()
    {
        await using var context = CreateContext();
        var proposalId = Guid.NewGuid();
        var first = MakeProposalItem(proposalId, "First", DateTime.UtcNow.AddMinutes(-2));
        var second = MakeProposalItem(proposalId, "Second", DateTime.UtcNow);
        context.ProposalItemSet.AddRange(second, first);
        await context.SaveChangesAsync();
        var repository = new QuotationRepository(context);

        var result = await repository.GetProposalItemsAsync(proposalId);

        Assert.Collection(
            result,
            item => Assert.Equal("First", item.ItemName),
            item => Assert.Equal("Second", item.ItemName));
    }

    [Fact]
    public async Task AddItemAsync_AddsQuotationItemToContext()
    {
        await using var context = CreateContext();
        var repository = new QuotationRepository(context);
        var item = new QuotationItem
        {
            QuotationItemId = Guid.NewGuid(),
            QuotationId = Guid.NewGuid(),
            ItemName = "Counter",
            Quantity = 1,
            UnitPrice = 100m,
            GrossAmount = 100m,
            DiscountAmount = 0m,
            TotalAmount = 100m
        };

        await repository.AddItemAsync(item);
        await context.SaveChangesAsync();

        Assert.Contains(context.QuotationItemSet, stored => stored.QuotationItemId == item.QuotationItemId);
    }

    [Fact]
    public async Task UpdateItem_UpdatesQuotationItem()
    {
        await using var context = CreateContext();
        var item = MakeQuotationItem(Guid.NewGuid(), "Old");
        context.QuotationItemSet.Add(item);
        await context.SaveChangesAsync();
        var repository = new QuotationRepository(context);

        item.ItemName = "New";
        repository.UpdateItem(item);
        await context.SaveChangesAsync();

        Assert.Equal("New", context.QuotationItemSet.Single(stored => stored.QuotationItemId == item.QuotationItemId).ItemName);
    }

    [Fact]
    public async Task RemoveItem_RemovesQuotationItem()
    {
        await using var context = CreateContext();
        var item = MakeQuotationItem(Guid.NewGuid(), "Remove me");
        context.QuotationItemSet.Add(item);
        await context.SaveChangesAsync();
        var repository = new QuotationRepository(context);

        repository.RemoveItem(item);
        await context.SaveChangesAsync();

        Assert.DoesNotContain(context.QuotationItemSet, stored => stored.QuotationItemId == item.QuotationItemId);
    }

    [Fact]
    public async Task AddOrderAsync_AddsOrderAndOrderItemToContext()
    {
        await using var context = CreateContext();
        var repository = new QuotationRepository(context);
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-TEST",
            CustomerId = Guid.NewGuid(),
            VatRate = 0.08m,
            VatAmount = 8m,
            OriginalTotalAmount = 108m,
            FinalTotalAmount = 108m,
            Status = OrderStatus.DEPOSIT_PENDING
        };
        var item = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Quantity = 1,
            Status = OrderItemStatus.PENDING,
            UnitPrice = 100m,
            DiscountAmount = 0m,
            SubtotalAmount = 100m
        };

        await repository.AddOrderAsync(order);
        await repository.AddOrderItemAsync(item);
        await context.SaveChangesAsync();

        Assert.Contains(context.OrderSet, stored => stored.OrderId == order.OrderId);
        Assert.Contains(context.OrderItemSet, stored => stored.OrderItemId == item.OrderItemId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static void SeedProject(
        AppDbContext context,
        Guid projectId,
        ProjectStatus status = ProjectStatus.PROPOSAL_SELECTED)
    {
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = Guid.NewGuid(),
            AssignedDesignerId = Guid.NewGuid(),
            ProjectName = "Cafe",
            Status = status
        });
    }

    private static Quotation MakeQuotation(
        Guid projectId,
        QuotationStatus status,
        Guid? proposalId = null,
        int versionNo = 1)
    {
        return new Quotation
        {
            QuotationId = Guid.NewGuid(),
            ProjectId = projectId,
            ProposalId = proposalId ?? Guid.NewGuid(),
            QuotationCode = Guid.NewGuid().ToString("N")[..12],
            VersionNo = versionNo,
            SubtotalAmount = 100m,
            TotalDiscountAmount = 0m,
            PreVatAmount = 100m,
            VatRate = 0.08m,
            VatAmount = 8m,
            TotalAmount = 108m,
            DepositAmount = 32m,
            Currency = "VND",
            Status = status,
            CreatedAt = DateTime.UtcNow.AddMinutes(versionNo)
        };
    }

    private static Proposal MakeProposal(Guid projectId, Guid proposalId, ProposalStatus status)
    {
        return new Proposal
        {
            ProposalId = proposalId,
            ProjectId = projectId,
            ProposalName = "Proposal",
            Status = status
        };
    }

    private static ProposalItem MakeProposalItem(Guid proposalId, string itemName, DateTime createdAt)
    {
        return new ProposalItem
        {
            ProposalItemId = Guid.NewGuid(),
            ProposalId = proposalId,
            ItemName = itemName,
            Quantity = 1,
            UnitPriceSnapshot = 100m,
            TotalPriceSnapshot = 100m,
            CreatedAt = createdAt
        };
    }

    private static QuotationItem MakeQuotationItem(Guid quotationId, string itemName)
    {
        return new QuotationItem
        {
            QuotationItemId = Guid.NewGuid(),
            QuotationId = quotationId,
            ItemName = itemName,
            Quantity = 1,
            UnitPrice = 100m,
            GrossAmount = 100m,
            DiscountAmount = 0m,
            TotalAmount = 100m
        };
    }
}
