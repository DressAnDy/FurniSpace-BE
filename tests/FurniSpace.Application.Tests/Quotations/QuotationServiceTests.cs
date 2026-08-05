#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Quotations;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.DTOs.Quotations;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.Quotations;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.ReadModels.Quotations;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Quotations;

public sealed class QuotationServiceTests
{
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _salesId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();
    private readonly Guid _proposalId = Guid.NewGuid();

    [Fact]
    public void QuotationItemFinancialCalculator_WhenItemHasDiscountAndTax_CalculatesExpectedAmounts()
    {
        var calculator = new QuotationItemFinancialCalculator();
        var item = new QuotationItem
        {
            Quantity = 2,
            UnitPrice = 100m,
            CustomizationAdditionalCost = 20m,
            DiscountAmount = 40m,
            TaxRate = 10m
        };

        calculator.Calculate(item);

        Assert.Equal(240m, item.GrossAmount);
        Assert.Equal(200m, item.TaxableAmount);
        Assert.Equal(20m, item.TaxAmount);
        Assert.Equal(220m, item.TotalAmount);
        Assert.Equal(220m, item.SubtotalAmount);
    }

    [Fact]
    public void QuotationRecalculationService_WhenItemsExist_AggregatesHeaderTotals()
    {
        var service = CreateRecalculationService();
        var quotation = new Quotation();
        var items = new List<QuotationItem>
        {
            new()
            {
                QuotationItemId = Guid.NewGuid(),
                Quantity = 2,
                UnitPrice = 100m,
                CustomizationAdditionalCost = 20m,
                DiscountAmount = 40m,
                TaxRate = 10m
            },
            new()
            {
                QuotationItemId = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 50m,
                DiscountAmount = 5m,
                TaxRate = 0m
            }
        };

        service.Recalculate(quotation, items);

        Assert.Equal(290m, quotation.SubtotalAmount);
        Assert.Equal(45m, quotation.DiscountAmount);
        Assert.Equal(245m, quotation.TaxableAmount);
        Assert.Equal(20m, quotation.TaxAmount);
        Assert.Equal(265m, quotation.TotalAmount);
        Assert.Equal("VND", quotation.Currency);
    }

    [Fact]
    public async Task GetByProjectAsync_CustomerSeesOnlyAvailableQuotations()
    {
        var quotations = new FakeQuotationRepository();
        quotations.ProjectQuotations.Add(MakeQuotation(QuotationStatus.DRAFT));
        quotations.ProjectQuotations.Add(MakeQuotation(QuotationStatus.SENT));
        quotations.ProjectQuotations.Add(MakeQuotation(QuotationStatus.CANCELLED));
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER" });

        var result = await service.GetByProjectAsync(_projectId, _customerId, new QuotationQueryDto());

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(QuotationStatus.SENT, item.Status);
    }

    [Fact]
    public async Task GetByProjectAsync_SalesSeesAllQuotations()
    {
        var quotations = new FakeQuotationRepository();
        quotations.ProjectQuotations.Add(MakeQuotation(QuotationStatus.DRAFT));
        quotations.ProjectQuotations.Add(MakeQuotation(QuotationStatus.SENT));
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.GetByProjectAsync(_projectId, _salesId, new QuotationQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.Data!.Items.Count);
    }

    [Fact]
    public async Task GetByProjectAsync_WhenProjectMissing_ReturnsProjectNotFound()
    {
        var service = BuildService(new() { ProjectExists = false });

        var result = await service.GetByProjectAsync(_projectId, _salesId, new QuotationQueryDto());

        Assert.Equal(404, result.Status);
        Assert.Equal(QuotationErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetByProjectAsync_WhenUserHasNoAccess_ReturnsForbidden()
    {
        var service = BuildService(new() { Role = "DESIGNER" });

        var result = await service.GetByProjectAsync(_projectId, Guid.NewGuid(), new QuotationQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_WhenAuthorized_ReturnsItems()
    {
        var detail = MakeDetail(QuotationStatus.SENT);
        detail.Items = [new QuotationItemReadModel { QuotationItemId = Guid.NewGuid(), ItemName = "Counter" }];
        var service = BuildService(new() { Quotations = new FakeQuotationRepository { Detail = detail }, Role = "SALES" });

        var result = await service.GetDetailAsync(detail.QuotationId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task GetDetailAsync_WhenCustomerViewsDraft_ReturnsQuotationNotAvailable()
    {
        var service = BuildService(new() { Quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) }, Role = "CUSTOMER" });

        var result = await service.GetDetailAsync(Guid.NewGuid(), _customerId);

        Assert.Equal(403, result.Status);
        Assert.Equal(QuotationErrorCodes.QuotationNotAvailable, result.ErrorCode);
    }

    [Fact]
    public async Task GetDetailAsync_WhenMissing_ReturnsQuotationNotFound()
    {
        var service = BuildService(new() { Role = "ADMIN" });

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(QuotationErrorCodes.QuotationNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetDetailAsync_WhenQuotationExpired_MarksAsExpired()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        quotation.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var detail = MakeAcceptReadyDetail(quotation);
        var quotations = new FakeQuotationRepository { Detail = detail };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER" });

        var result = await service.GetDetailAsync(quotation.QuotationId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(QuotationStatus.EXPIRED, quotation.Status);
        Assert.Equal(QuotationStatus.EXPIRED, result.Data!.Status);
    }

    [Fact]
    public async Task CreateDraftAsync_WhenValid_CreatesQuotationAndItems()
    {
        var quotations = new FakeQuotationRepository { SelectedProposal = MakeSelectedProposal() };
        quotations.ProposalItems.Add(new ProposalItem
        {
            ProposalItemId = Guid.NewGuid(),
            ProposalId = _proposalId,
            ProductVersionId = Guid.NewGuid(),
            ItemName = "Coffee Counter",
            Quantity = 2,
            UnitPriceSnapshot = 100m,
            TotalPriceSnapshot = 999m,
            IsCustomized = true,
            Note = "Wood"
        });
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.CreateDraftAsync(_projectId, _salesId);

        Assert.Equal(201, result.Status);
        Assert.Single(quotations.AddedQuotations);
        Assert.Single(quotations.AddedItems);
        Assert.Equal(QuotationStatus.DRAFT, quotations.AddedQuotations[0].Status);
        Assert.Equal(200m, quotations.AddedQuotations[0].SubtotalAmount);
        Assert.Equal(0m, quotations.AddedQuotations[0].DiscountAmount);
        Assert.Equal(200m, quotations.AddedQuotations[0].TaxableAmount);
        Assert.Equal(0m, quotations.AddedQuotations[0].TaxAmount);
        Assert.Equal(200m, quotations.AddedQuotations[0].TotalAmount);
        Assert.Equal(QuotationItemType.PRODUCT_ITEM, quotations.AddedItems[0].ItemType);
        Assert.Equal(200m, quotations.AddedItems[0].GrossAmount);
        Assert.Equal(200m, quotations.AddedItems[0].TaxableAmount);
        Assert.Equal(0m, quotations.AddedItems[0].TaxAmount);
        Assert.Equal(200m, quotations.AddedItems[0].TotalAmount);
    }

    [Fact]
    public async Task CreateDraftAsync_WhenProjectStatusInvalid_ReturnsProjectNotReady()
    {
        var project = MakeProject();
        project.Status = ProjectStatus.IN_CONSULTATION;
        var service = BuildService(new() { Project = project, Role = "SALES" });

        var result = await service.CreateDraftAsync(_projectId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.ProjectNotReadyForQuotation, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDraftAsync_WhenProposalNotSelected_ReturnsProposalNotSelected()
    {
        var service = BuildService(new() { Role = "SALES" });

        var result = await service.CreateDraftAsync(_projectId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.ProposalNotSelected, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDraftAsync_WhenPendingCustomization_ReturnsPendingError()
    {
        var quotations = new FakeQuotationRepository { SelectedProposal = MakeSelectedProposal() };
        var service = BuildService(new() { Quotations = quotations, Role = "SALES", HasPendingCustomization = true });

        var result = await service.CreateDraftAsync(_projectId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(CustomizationRequestErrorCodes.CustomizationRequestPending, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDraftAsync_WhenQuotationExists_ReturnsAlreadyExists()
    {
        var quotations = new FakeQuotationRepository
        {
            SelectedProposal = MakeSelectedProposal(),
            HasExistingQuotation = true
        };
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.CreateDraftAsync(_projectId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.QuotationAlreadyExists, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDraftAsync_WhenSalesUnassigned_ReturnsForbidden()
    {
        var service = BuildService(new() { Role = "SALES" });

        var result = await service.CreateDraftAsync(_projectId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateDraftAsync_WhenSaveFails_RollsBack()
    {
        var quotations = new FakeQuotationRepository { SelectedProposal = MakeSelectedProposal() };
        var rollbackCalled = false;
        var service = BuildService(new() { Quotations = quotations, Role = "SALES", UnitOfWork = TestUnitOfWork.ForTransaction(
                _ => Task.CompletedTask,
                _ => throw new InvalidOperationException("save failed"),
                _ => Task.CompletedTask,
                _ =>
                {
                    rollbackCalled = true;
                    return Task.CompletedTask;
                }) });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateDraftAsync(_projectId, _salesId));
        Assert.True(rollbackCalled);
    }

    [Fact]
    public async Task UpdateAsync_WhenDraft_RecalculatesTotalsAndUpdatesNotes()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 200m));
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.UpdateAsync(
            quotation.QuotationId,
            _salesId,
            new UpdateQuotationRequestDto
            {
                CustomerNote = " Customer note ",
                SalesNote = " Ready "
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("Customer note", quotation.CustomerNote);
        Assert.Equal("Ready", quotation.SalesNote);
        Assert.Equal(200m, quotation.SubtotalAmount);
        Assert.Equal(0m, quotation.DiscountAmount);
        Assert.Equal(200m, quotation.TaxableAmount);
        Assert.Equal(0m, quotation.TaxAmount);
        Assert.Equal(200m, quotation.TotalAmount);
    }

    [Fact]
    public async Task UpdateAsync_WhenSent_ReturnsInvalidStatus()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.SENT) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.UpdateAsync(quotation.QuotationId, _salesId, new UpdateQuotationRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationStatus, result.ErrorCode);
    }

    [Fact]
    public async Task AddManualItemAsync_WhenValid_AddsManualItemAndRecalculatesTotals()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.REVISED);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.REVISED) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 200m));
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.AddManualItemAsync(
            quotation.QuotationId,
            _salesId,
            new CreateManualQuotationItemRequestDto
            {
                ItemName = " Delivery ",
                Quantity = 2,
                UnitPrice = 30m,
                DiscountAmount = 5m,
                TaxRate = 10m,
                DisplayOrder = 3
            });

        Assert.Equal(200, result.Status);
        var item = Assert.Single(quotations.AddedItems.Where(added => added.ItemType == QuotationItemType.MANUAL_ITEM));
        Assert.Equal("Delivery", item.ItemName);
        Assert.Equal(3, item.DisplayOrder);
        Assert.Equal(60m, item.GrossAmount);
        Assert.Equal(55m, item.TaxableAmount);
        Assert.Equal(5.5m, item.TaxAmount);
        Assert.Equal(60.5m, item.TotalAmount);
        Assert.Equal(60.5m, item.SubtotalAmount);
        Assert.Equal(260m, quotation.SubtotalAmount);
        Assert.Equal(5m, quotation.DiscountAmount);
        Assert.Equal(255m, quotation.TaxableAmount);
        Assert.Equal(5.5m, quotation.TaxAmount);
        Assert.Equal(260.5m, quotation.TotalAmount);
    }

    [Fact]
    public async Task AddManualItemAsync_WhenInvalidRequest_ReturnsInvalidItem()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.AddManualItemAsync(
            quotation.QuotationId,
            _salesId,
            new CreateManualQuotationItemRequestDto { ItemName = " ", Quantity = 0, UnitPrice = 10m });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationItem, result.ErrorCode);
    }

    [Fact]
    public async Task AddManualItemAsync_WhenDiscountExceedsGross_ReturnsInvalidItem()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.AddManualItemAsync(
            quotation.QuotationId,
            _salesId,
            new CreateManualQuotationItemRequestDto
            {
                ItemName = "Shipping",
                Quantity = 1,
                UnitPrice = 10m,
                DiscountAmount = 11m
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationItem, result.ErrorCode);
    }

    [Fact]
    public async Task AddManualItemAsync_WhenTaxRateInvalid_ReturnsInvalidItem()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.AddManualItemAsync(
            quotation.QuotationId,
            _salesId,
            new CreateManualQuotationItemRequestDto
            {
                ItemName = "Shipping",
                Quantity = 1,
                UnitPrice = 10m,
                TaxRate = 101m
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationItem, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateManualItemAsync_WhenProductItem_UpdatesFinancialInputs()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var item = MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 100m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(item);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.UpdateManualItemAsync(
            quotation.QuotationId,
            item.QuotationItemId,
            _salesId,
            new UpdateManualQuotationItemRequestDto
            {
                ItemName = "Premium Product",
                Quantity = 2,
                UnitPrice = 100m,
                CustomizationUnitAdditionalCost = 20m,
                DiscountAmount = 40m,
                TaxRate = 10m,
                DisplayOrder = 5
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("Premium Product", item.ItemName);
        Assert.Equal(5, item.DisplayOrder);
        Assert.Equal(20m, item.CustomizationAdditionalCost);
        Assert.Equal(240m, item.GrossAmount);
        Assert.Equal(200m, item.TaxableAmount);
        Assert.Equal(20m, item.TaxAmount);
        Assert.Equal(220m, item.TotalAmount);
        Assert.Equal(220m, quotation.TotalAmount);
    }

    [Fact]
    public async Task UpdateManualItemAsync_WhenValid_UpdatesItemAndTotals()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var item = MakeQuotationItem(quotation.QuotationId, QuotationItemType.MANUAL_ITEM, subtotal: 50m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(item);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.UpdateManualItemAsync(
            quotation.QuotationId,
            item.QuotationItemId,
            _salesId,
            new UpdateManualQuotationItemRequestDto
            {
                ItemName = "Installation",
                Quantity = 3,
                UnitPrice = 20m,
                CustomizationUnitAdditionalCost = 999m,
                DiscountAmount = 10m,
                TaxRate = 8m
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("Installation", item.ItemName);
        Assert.Equal(0m, item.CustomizationAdditionalCost);
        Assert.Null(item.ProposalItemId);
        Assert.Null(item.ProductVersionId);
        Assert.False(item.IsCustomized);
        Assert.Equal(60m, item.GrossAmount);
        Assert.Equal(50m, item.TaxableAmount);
        Assert.Equal(4m, item.TaxAmount);
        Assert.Equal(54m, item.TotalAmount);
        Assert.Equal(54m, item.SubtotalAmount);
        Assert.Equal(54m, quotation.TotalAmount);
    }

    [Fact]
    public async Task UpdateManualItemAsync_WhenItemMissing_ReturnsItemNotFound()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.UpdateManualItemAsync(
            quotation.QuotationId,
            Guid.NewGuid(),
            _salesId,
            new UpdateManualQuotationItemRequestDto { ItemName = "Delivery" });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.QuotationItemNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateItemFinancialsAsync_WhenProductItemValid_RecalculatesItemAndHeader()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var item = MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 100m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(item);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.UpdateItemFinancialsAsync(
            quotation.QuotationId,
            item.QuotationItemId,
            _salesId,
            new UpdateQuotationItemFinancialsRequestDto
            {
                Quantity = 2,
                UnitPrice = 100m,
                CustomizationUnitAdditionalCost = 20m,
                DiscountAmount = 40m,
                TaxRate = 10m
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("Item", item.ItemName);
        Assert.Equal(240m, item.GrossAmount);
        Assert.Equal(200m, item.TaxableAmount);
        Assert.Equal(20m, item.TaxAmount);
        Assert.Equal(220m, item.TotalAmount);
        Assert.Equal(220m, quotation.TotalAmount);
    }

    [Fact]
    public async Task UpdateItemFinancialsAsync_WhenManualItemIgnoresCustomizationCost_RecalculatesWithZeroCustomization()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var item = MakeQuotationItem(quotation.QuotationId, QuotationItemType.MANUAL_ITEM, subtotal: 50m);
        item.ProposalItemId = Guid.NewGuid();
        item.ProductVersionId = Guid.NewGuid();
        item.IsCustomized = true;
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(item);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.UpdateItemFinancialsAsync(
            quotation.QuotationId,
            item.QuotationItemId,
            _salesId,
            new UpdateQuotationItemFinancialsRequestDto
            {
                Quantity = 1,
                UnitPrice = 100m,
                CustomizationUnitAdditionalCost = 999m,
                DiscountAmount = 10m,
                TaxRate = 10m
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(0m, item.CustomizationAdditionalCost);
        Assert.Null(item.ProposalItemId);
        Assert.Null(item.ProductVersionId);
        Assert.False(item.IsCustomized);
        Assert.Equal(100m, item.GrossAmount);
        Assert.Equal(90m, item.TaxableAmount);
        Assert.Equal(9m, item.TaxAmount);
        Assert.Equal(99m, item.TotalAmount);
    }

    [Fact]
    public async Task UpdateItemFinancialsAsync_WhenQuotationSent_ReturnsInvalidStatus()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        var item = MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 100m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.SENT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(item);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.UpdateItemFinancialsAsync(
            quotation.QuotationId,
            item.QuotationItemId,
            _salesId,
            new UpdateQuotationItemFinancialsRequestDto { Quantity = 2 });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationStatus, result.ErrorCode);
    }

    [Fact]
    public async Task BulkUpdateItemFinancialsAsync_WhenValid_UpdatesAllItemsAndCommits()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var productItem = MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 100m);
        var manualItem = MakeQuotationItem(quotation.QuotationId, QuotationItemType.MANUAL_ITEM, subtotal: 50m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.AddRange([productItem, manualItem]);
        var committed = false;
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => Task.CompletedTask,
            _ => Task.FromResult(1),
            _ =>
            {
                committed = true;
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES", UnitOfWork = unitOfWork });

        var result = await service.BulkUpdateItemFinancialsAsync(
            quotation.QuotationId,
            _salesId,
            new BulkUpdateQuotationItemFinancialsRequestDto
            {
                Items =
                [
                    new()
                    {
                        QuotationItemId = productItem.QuotationItemId,
                        Quantity = 2,
                        UnitPrice = 100m,
                        CustomizationUnitAdditionalCost = 20m,
                        DiscountAmount = 40m,
                        TaxRate = 10m
                    },
                    new()
                    {
                        QuotationItemId = manualItem.QuotationItemId,
                        Quantity = 1,
                        UnitPrice = 50m,
                        CustomizationUnitAdditionalCost = 999m,
                        DiscountAmount = 5m,
                        TaxRate = 0m
                    }
                ]
            });

        Assert.Equal(200, result.Status);
        Assert.True(committed);
        Assert.Equal(220m, productItem.TotalAmount);
        Assert.Equal(45m, manualItem.TotalAmount);
        Assert.Equal(290m, quotation.SubtotalAmount);
        Assert.Equal(45m, quotation.DiscountAmount);
        Assert.Equal(265m, quotation.TotalAmount);
    }

    [Fact]
    public async Task BulkUpdateItemFinancialsAsync_WhenOneItemInvalid_ReturnsInvalidWithoutChangingItems()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var productItem = MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 100m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(productItem);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.BulkUpdateItemFinancialsAsync(
            quotation.QuotationId,
            _salesId,
            new BulkUpdateQuotationItemFinancialsRequestDto
            {
                Items =
                [
                    new()
                    {
                        QuotationItemId = productItem.QuotationItemId,
                        Quantity = 1,
                        UnitPrice = 10m,
                        DiscountAmount = 11m
                    }
                ]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationItem, result.ErrorCode);
        Assert.Equal(100m, productItem.UnitPrice);
        Assert.Null(productItem.TotalAmount);
    }

    [Fact]
    public async Task BulkUpdateItemFinancialsAsync_WhenRequestHasDuplicateItems_ReturnsInvalid()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var item = MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 100m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(item);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.BulkUpdateItemFinancialsAsync(
            quotation.QuotationId,
            _salesId,
            new BulkUpdateQuotationItemFinancialsRequestDto
            {
                Items =
                [
                    new() { QuotationItemId = item.QuotationItemId, Quantity = 1, UnitPrice = 10m },
                    new() { QuotationItemId = item.QuotationItemId, Quantity = 1, UnitPrice = 10m }
                ]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationItem, result.ErrorCode);
    }

    [Fact]
    public async Task BulkUpdateItemFinancialsAsync_WhenItemMissing_ReturnsItemNotFound()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.BulkUpdateItemFinancialsAsync(
            quotation.QuotationId,
            _salesId,
            new BulkUpdateQuotationItemFinancialsRequestDto
            {
                Items = [new() { QuotationItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m }]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.QuotationItemNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task BulkUpdateItemFinancialsAsync_WhenSaveFails_RollsBack()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var item = MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 100m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(item);
        var rollbackCalled = false;
        var service = BuildService(new()
        {
            Quotations = quotations,
            Role = "SALES",
            UnitOfWork = TestUnitOfWork.ForTransaction(
                _ => Task.CompletedTask,
                _ => throw new InvalidOperationException("save failed"),
                _ => Task.CompletedTask,
                _ =>
                {
                    rollbackCalled = true;
                    return Task.CompletedTask;
                })
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BulkUpdateItemFinancialsAsync(
            quotation.QuotationId,
            _salesId,
            new BulkUpdateQuotationItemFinancialsRequestDto
            {
                Items = [new() { QuotationItemId = item.QuotationItemId, Quantity = 2, UnitPrice = 100m }]
            }));

        Assert.True(rollbackCalled);
    }

    [Fact]
    public async Task SendAsync_WhenReady_SendsQuotationAndNotifiesCustomer()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        quotation.TotalAmount = 250m;
        quotation.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var detail = MakeDetail(QuotationStatus.DRAFT);
        detail.QuotationId = quotation.QuotationId;
        detail.QuotationCode = quotation.QuotationCode;
        detail.TotalAmount = quotation.TotalAmount;
        detail.ValidUntil = quotation.ValidUntil;
        detail.Items = [new QuotationItemReadModel { QuotationItemId = Guid.NewGuid(), QuotationId = quotation.QuotationId }];
        var quotations = new FakeQuotationRepository { Detail = detail };
        quotations.AddedQuotations.Add(quotation);
        var dispatcher = new FakeNotificationDispatcher();
        var service = BuildService(new() { Quotations = quotations, Role = "SALES", Notifications = dispatcher });

        var result = await service.SendAsync(quotation.QuotationId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(QuotationStatus.SENT, quotation.Status);
        Assert.Equal(ProjectStatus.QUOTATION_SENT, ProjectEntity!.Status);
        Assert.Equal(NotificationType.QuotationSent, dispatcher.LastType);
        Assert.Contains(_customerId, dispatcher.LastReceiverIds);
    }

    [Fact]
    public async Task SendAsync_WhenQuotationIncomplete_ReturnsNotReady()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var detail = MakeDetail(QuotationStatus.DRAFT);
        detail.QuotationId = quotation.QuotationId;
        detail.TotalAmount = 0m;
        detail.Items = [];
        var quotations = new FakeQuotationRepository { Detail = detail };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.SendAsync(quotation.QuotationId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.QuotationNotReadyToSend, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteManualItemAsync_WhenValid_RemovesItemAndRecalculatesTotals()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var manualItem = MakeQuotationItem(quotation.QuotationId, QuotationItemType.MANUAL_ITEM, subtotal: 50m);
        var productItem = MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 200m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.AddRange([manualItem, productItem]);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.DeleteManualItemAsync(quotation.QuotationId, manualItem.QuotationItemId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.DoesNotContain(quotations.AddedItems, item => item.QuotationItemId == manualItem.QuotationItemId);
        Assert.Equal(200m, quotation.TotalAmount);
    }

    [Fact]
    public async Task DeleteManualItemAsync_WhenProductItem_ReturnsNotEditable()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var productItem = MakeQuotationItem(quotation.QuotationId, QuotationItemType.PRODUCT_ITEM, subtotal: 200m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.DRAFT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(productItem);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.DeleteManualItemAsync(quotation.QuotationId, productItem.QuotationItemId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.QuotationItemNotEditable, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteManualItemAsync_WhenSent_ReturnsInvalidStatus()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        var manualItem = MakeQuotationItem(quotation.QuotationId, QuotationItemType.MANUAL_ITEM, subtotal: 50m);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.SENT) };
        quotations.AddedQuotations.Add(quotation);
        quotations.AddedItems.Add(manualItem);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.DeleteManualItemAsync(quotation.QuotationId, manualItem.QuotationItemId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationStatus, result.ErrorCode);
    }

    [Fact]
    public async Task AcceptAsync_WhenValid_CreatesOrderItemsAndNotifiesSales()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        quotation.TotalAmount = 250m;
        quotation.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var detail = MakeAcceptReadyDetail(quotation);
        var quotations = new FakeQuotationRepository { Detail = detail };
        quotations.AddedQuotations.Add(quotation);
        var orders = new FakeOrderRepository();
        var dispatcher = new FakeNotificationDispatcher();
        var service = BuildService(new() { Quotations = quotations, Orders = orders, Role = "CUSTOMER", Notifications = dispatcher });

        var result = await service.AcceptAsync(quotation.QuotationId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(QuotationStatus.ACCEPTED, quotation.Status);
        Assert.Equal(ProjectStatus.ORDER_CONFIRMED, ProjectEntity!.Status);
        var order = Assert.Single(orders.AddedOrders);
        Assert.Equal(OrderStatus.DEPOSIT_PENDING, order.Status);
        Assert.Equal(250m, order.FinalTotalAmount);
        Assert.Equal(75m, order.DepositAmount);
        Assert.Equal(_customerId, order.ConfirmedBy);
        Assert.Equal(2, orders.AddedOrderItems.Count);
        Assert.All(orders.AddedOrderItems, item => Assert.Equal(OrderItemStatus.PENDING, item.Status));
        Assert.Equal(NotificationType.QuotationAccepted, dispatcher.LastType);
        Assert.Contains(_salesId, dispatcher.LastReceiverIds);
    }

    [Fact]
    public async Task AcceptAsync_WhenUserIsNotOwnerCustomer_ReturnsForbidden()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        var orders = new FakeOrderRepository();
        var quotations = new FakeQuotationRepository { Detail = MakeAcceptReadyDetail(quotation) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Orders = orders, Role = "SALES" });

        var result = await service.AcceptAsync(quotation.QuotationId, _salesId);

        Assert.Equal(403, result.Status);
        Assert.Empty(orders.AddedOrders);
    }

    [Fact]
    public async Task AcceptAsync_WhenStatusInvalid_ReturnsInvalidStatus()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.DRAFT);
        var detail = MakeAcceptReadyDetail(quotation);
        detail.Status = QuotationStatus.DRAFT;
        var quotations = new FakeQuotationRepository { Detail = detail };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER" });

        var result = await service.AcceptAsync(quotation.QuotationId, _customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationStatus, result.ErrorCode);
    }

    [Fact]
    public async Task AcceptAsync_WhenExpired_ReturnsQuotationExpired()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.REVISED);
        var detail = MakeAcceptReadyDetail(quotation);
        detail.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var quotations = new FakeQuotationRepository { Detail = detail };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER" });

        var result = await service.AcceptAsync(quotation.QuotationId, _customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.QuotationExpired, result.ErrorCode);
    }

    [Fact]
    public async Task AcceptAsync_WhenRevised_AllowsAcceptAndCreatesOrder()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.REVISED);
        quotation.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var orders = new FakeOrderRepository();
        var quotations = new FakeQuotationRepository { Detail = MakeAcceptReadyDetail(quotation) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Orders = orders, Role = "CUSTOMER" });

        var result = await service.AcceptAsync(quotation.QuotationId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Single(orders.AddedOrders);
    }

    [Fact]
    public async Task AcceptAsync_WhenOrderAlreadyExists_ReturnsOrderAlreadyCreated()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        var orders = new FakeOrderRepository { OrderExistsForQuotation = true };
        var quotations = new FakeQuotationRepository { Detail = MakeAcceptReadyDetail(quotation) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Orders = orders, Role = "CUSTOMER" });

        var result = await service.AcceptAsync(quotation.QuotationId, _customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.OrderAlreadyCreated, result.ErrorCode);
    }

    [Fact]
    public async Task AcceptAsync_WhenSaveFails_RollsBackAndDoesNotNotify()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        var quotations = new FakeQuotationRepository { Detail = MakeAcceptReadyDetail(quotation) };
        quotations.AddedQuotations.Add(quotation);
        var dispatcher = new FakeNotificationDispatcher();
        var rollbackCalled = false;
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER", UnitOfWork = TestUnitOfWork.ForTransaction(
                _ => Task.CompletedTask,
                _ => throw new InvalidOperationException("save failed"),
                _ => Task.CompletedTask,
                _ =>
                {
                    rollbackCalled = true;
                    return Task.CompletedTask;
                }), Notifications = dispatcher });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcceptAsync(quotation.QuotationId, _customerId));

        Assert.True(rollbackCalled);
        Assert.Null(dispatcher.LastType);
    }

    [Fact]
    public async Task RequestRevisionAsync_WhenValid_UpdatesQuotationProjectAndNotifiesSales()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        quotation.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var quotations = new FakeQuotationRepository { Detail = MakeAcceptReadyDetail(quotation) };
        quotations.AddedQuotations.Add(quotation);
        var dispatcher = new FakeNotificationDispatcher();
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER", Notifications = dispatcher });

        var result = await service.RequestRevisionAsync(
            quotation.QuotationId,
            _customerId,
            new RequestQuotationRevisionDto { RevisionReason = " Update delivery date. " });

        Assert.Equal(200, result.Status);
        Assert.Equal(QuotationStatus.REVISION_REQUESTED, quotation.Status);
        Assert.Equal("Update delivery date.", quotation.RevisionReason);
        Assert.Equal(ProjectStatus.QUOTATION_REVISION_REQUESTED, ProjectEntity!.Status);
        Assert.Equal(NotificationType.QuotationRevisionRequested, dispatcher.LastType);
        Assert.Contains(_salesId, dispatcher.LastReceiverIds);
    }

    [Fact]
    public async Task RequestRevisionAsync_WhenReasonMissing_ReturnsInvalidReason()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        var quotations = new FakeQuotationRepository { Detail = MakeAcceptReadyDetail(quotation) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER" });

        var result = await service.RequestRevisionAsync(
            quotation.QuotationId,
            _customerId,
            new RequestQuotationRevisionDto { RevisionReason = " " });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationRevisionReason, result.ErrorCode);
    }

    [Fact]
    public async Task RequestRevisionAsync_WhenExpired_ReturnsQuotationExpired()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        quotation.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var quotations = new FakeQuotationRepository { Detail = MakeAcceptReadyDetail(quotation) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER" });

        var result = await service.RequestRevisionAsync(
            quotation.QuotationId,
            _customerId,
            new RequestQuotationRevisionDto { RevisionReason = "Too high." });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.QuotationExpired, result.ErrorCode);
        Assert.Equal(QuotationStatus.EXPIRED, quotation.Status);
    }

    [Fact]
    public async Task ReviseAsync_WhenRevisionRequested_IncrementsVersionAndMarksRevised()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.REVISION_REQUESTED);
        quotation.VersionNo = 2;
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.REVISION_REQUESTED) };
        quotations.Detail!.QuotationId = quotation.QuotationId;
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.ReviseAsync(quotation.QuotationId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(3, quotation.VersionNo);
        Assert.Equal(QuotationStatus.REVISED, quotation.Status);
    }

    [Fact]
    public async Task ReviseAsync_WhenStatusInvalid_ReturnsInvalidStatus()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.SENT) };
        quotations.Detail!.QuotationId = quotation.QuotationId;
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.ReviseAsync(quotation.QuotationId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationStatus, result.ErrorCode);
    }

    [Fact]
    public async Task CancelAsync_WhenRevised_MarksQuotationCancelled()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.REVISED);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.REVISED) };
        quotations.Detail!.QuotationId = quotation.QuotationId;
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.CancelAsync(quotation.QuotationId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(QuotationStatus.CANCELLED, quotation.Status);
    }

    [Fact]
    public async Task CancelAsync_WhenSent_ReturnsInvalidStatus()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        var quotations = new FakeQuotationRepository { Detail = MakeDetail(QuotationStatus.SENT) };
        quotations.Detail!.QuotationId = quotation.QuotationId;
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "SALES" });

        var result = await service.CancelAsync(quotation.QuotationId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationStatus, result.ErrorCode);
    }

    [Fact]
    public async Task RejectAsync_WhenValid_RejectsQuotationAndNotifiesSales()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        quotation.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var quotations = new FakeQuotationRepository { Detail = MakeAcceptReadyDetail(quotation) };
        quotations.AddedQuotations.Add(quotation);
        var dispatcher = new FakeNotificationDispatcher();
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER", Notifications = dispatcher });

        var result = await service.RejectAsync(
            quotation.QuotationId,
            _customerId,
            new RejectQuotationRequestDto { RejectReason = " Price is too high. " });

        Assert.Equal(200, result.Status);
        Assert.Equal(QuotationStatus.REJECTED, quotation.Status);
        Assert.Equal("Price is too high.", quotation.RejectReason);
        Assert.NotNull(quotation.RejectedAt);
        Assert.Equal(ProjectStatus.PROPOSAL_SELECTED, ProjectEntity!.Status);
        Assert.Empty(quotations.AddedOrders);
        Assert.Equal(NotificationType.QuotationRejected, dispatcher.LastType);
        Assert.Contains(_salesId, dispatcher.LastReceiverIds);
    }

    [Fact]
    public async Task RejectAsync_WhenReasonMissing_ReturnsInvalidReason()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        var quotations = new FakeQuotationRepository { Detail = MakeAcceptReadyDetail(quotation) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER" });

        var result = await service.RejectAsync(
            quotation.QuotationId,
            _customerId,
            new RejectQuotationRequestDto { RejectReason = " " });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.InvalidQuotationRejectReason, result.ErrorCode);
    }

    [Fact]
    public async Task RejectAsync_WhenExpired_ReturnsQuotationExpired()
    {
        var quotation = MakeEntityQuotation(QuotationStatus.SENT);
        quotation.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var quotations = new FakeQuotationRepository { Detail = MakeAcceptReadyDetail(quotation) };
        quotations.AddedQuotations.Add(quotation);
        var service = BuildService(new() { Quotations = quotations, Role = "CUSTOMER" });

        var result = await service.RejectAsync(
            quotation.QuotationId,
            _customerId,
            new RejectQuotationRequestDto { RejectReason = "No longer needed." });

        Assert.Equal(400, result.Status);
        Assert.Equal(QuotationErrorCodes.QuotationExpired, result.ErrorCode);
        Assert.Equal(QuotationStatus.EXPIRED, quotation.Status);
    }

    private sealed class QuotationServiceTestOptions
    {
        public FakeQuotationRepository Quotations { get; init; } = new();

        public FakeOrderRepository? Orders { get; init; }

        public string Role { get; init; } = "ADMIN";

        public ProjectDetailReadModel? Project { get; init; }

        public bool ProjectExists { get; init; } = true;

        public bool HasPendingCustomization { get; init; }

        public bool OrderExistsForQuotation { get; init; }

        public FurniSpace.Infrastructure.Persistence.IUnitOfWork? UnitOfWork { get; init; }

        public INotificationDispatcher? Notifications { get; init; }
    }

    private QuotationService BuildService(QuotationServiceTestOptions? options = null)
    {
        options ??= new QuotationServiceTestOptions();
        var orderRepository = options.Orders ?? new FakeOrderRepository
        {
            OrderExistsForQuotation = options.OrderExistsForQuotation
        };
        var projectRepository = new FakeProjectRepository(
            options.ProjectExists ? options.Project ?? MakeProject() : null,
            options.Role);
        ProjectEntity = projectRepository.ProjectEntity;
        return new QuotationService(
            options.Quotations,
            projectRepository,
            orderRepository,
            new FakeCustomizationRequestRepository(options.HasPendingCustomization),
            new QuotationServiceDependencies(
                options.UnitOfWork ?? TestUnitOfWork.Instance,
                new OrderWorkflowSettings { DepositPercent = 30 },
                CreateRecalculationService(),
                options.Notifications,
                Logger: null));
    }

    private static QuotationRecalculationService CreateRecalculationService()
    {
        return new QuotationRecalculationService(
            new QuotationItemFinancialCalculator(),
            new QuotationFinancialSummaryCalculator());
    }

    private Project? ProjectEntity { get; set; }

    private ProjectDetailReadModel MakeProject()
    {
        return new ProjectDetailReadModel
        {
            ProjectId = _projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            AssignedDesignerId = _designerId,
            Status = ProjectStatus.PROPOSAL_SELECTED
        };
    }

    private QuotationReadModel MakeQuotation(QuotationStatus status)
    {
        return new QuotationReadModel
        {
            QuotationId = Guid.NewGuid(),
            ProjectId = _projectId,
            ProposalId = _proposalId,
            Status = status,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            AssignedDesignerId = _designerId
        };
    }

    private QuotationDetailReadModel MakeDetail(QuotationStatus status)
    {
        var quotation = MakeQuotation(status);
        return new QuotationDetailReadModel
        {
            QuotationId = quotation.QuotationId,
            ProjectId = quotation.ProjectId,
            ProposalId = quotation.ProposalId,
            Status = quotation.Status,
            CustomerId = quotation.CustomerId,
            AssignedSalesId = quotation.AssignedSalesId,
            AssignedDesignerId = quotation.AssignedDesignerId
        };
    }

    private QuotationDetailReadModel MakeAcceptReadyDetail(Quotation quotation)
    {
        return new QuotationDetailReadModel
        {
            QuotationId = quotation.QuotationId,
            ProjectId = quotation.ProjectId,
            ProposalId = quotation.ProposalId,
            QuotationCode = quotation.QuotationCode,
            TotalAmount = quotation.TotalAmount ?? 250m,
            Status = quotation.Status,
            ValidUntil = quotation.ValidUntil ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            AssignedDesignerId = _designerId,
            Items =
            [
                new QuotationItemReadModel
                {
                    QuotationItemId = Guid.NewGuid(),
                    QuotationId = quotation.QuotationId,
                    ItemName = "Counter",
                    ProductNameSnapshot = "Counter",
                    Quantity = 1,
                    UnitPrice = 100m,
                    GrossAmount = 100m,
                    DiscountAmount = 0m,
                    TaxableAmount = 100m,
                    TaxRate = 0m,
                    TaxAmount = 0m,
                    TotalAmount = 100m,
                    SubtotalAmount = 100m
                },
                new QuotationItemReadModel
                {
                    QuotationItemId = Guid.NewGuid(),
                    QuotationId = quotation.QuotationId,
                    ItemName = "Lighting",
                    Quantity = 1,
                    UnitPrice = 150m,
                    GrossAmount = 150m,
                    DiscountAmount = 0m,
                    TaxableAmount = 150m,
                    TaxRate = 0m,
                    TaxAmount = 0m,
                    TotalAmount = 150m,
                    SubtotalAmount = 150m
                }
            ]
        };
    }

    private Quotation MakeEntityQuotation(QuotationStatus status)
    {
        return new Quotation
        {
            QuotationId = Guid.NewGuid(),
            ProjectId = _projectId,
            ProposalId = _proposalId,
            QuotationCode = "QTN-TEST",
            Status = status,
            DiscountAmount = 0m,
            TaxAmount = 0m
        };
    }

    private static QuotationItem MakeQuotationItem(
        Guid quotationId,
        QuotationItemType itemType,
        decimal subtotal)
    {
        return new QuotationItem
        {
            QuotationItemId = Guid.NewGuid(),
            QuotationId = quotationId,
            ItemType = itemType,
            ItemName = "Item",
            DisplayOrder = 0,
            Quantity = 1,
            UnitPrice = subtotal,
            DiscountAmount = 0m,
            CustomizationAdditionalCost = 0m,
            TaxRate = 0m,
            SubtotalAmount = subtotal
        };
    }

    private SelectedProposalForQuotationReadModel MakeSelectedProposal()
    {
        return new SelectedProposalForQuotationReadModel
        {
            ProjectId = _projectId,
            ProposalId = _proposalId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            AssignedDesignerId = _designerId,
            ProjectStatus = ProjectStatus.PROPOSAL_SELECTED,
            ProposalStatus = ProposalStatus.SELECTED
        };
    }

    private sealed class FakeQuotationRepository : IQuotationRepository
    {
        public List<QuotationReadModel> ProjectQuotations { get; } = [];
        public List<ProposalItem> ProposalItems { get; } = [];
        public List<Quotation> AddedQuotations { get; } = [];
        public List<QuotationItem> AddedItems { get; } = [];
        public List<Order> AddedOrders { get; } = [];
        public List<OrderItem> AddedOrderItems { get; } = [];
        public QuotationDetailReadModel? Detail { get; init; }
        public SelectedProposalForQuotationReadModel? SelectedProposal { get; init; }
        public bool HasExistingQuotation { get; init; }

        public IQueryable<Quotation> Query() => AddedQuotations.AsQueryable();
        public Task<Quotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(AddedQuotations.FirstOrDefault(item => item.QuotationId == id));
        public Task<IReadOnlyList<Quotation>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Quotation>>(AddedQuotations);
        public Task AddAsync(Quotation entity, CancellationToken cancellationToken = default)
        {
            AddedQuotations.Add(entity);
            return Task.CompletedTask;
        }
        public Task AddRangeAsync(IEnumerable<Quotation> entities, CancellationToken cancellationToken = default)
        {
            AddedQuotations.AddRange(entities);
            return Task.CompletedTask;
        }
        public void Update(Quotation entity) { }
        public void Remove(Quotation entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<QuotationReadModel>> GetByProjectAsync(QuotationQueryReadModel query, CancellationToken cancellationToken = default)
        {
            var items = ProjectQuotations
                .Where(item => item.ProjectId == query.ProjectId)
                .Where(item => !query.Status.HasValue || item.Status == query.Status)
                .ToList();
            return Task.FromResult<IReadOnlyList<QuotationReadModel>>(items);
        }
        public Task<QuotationDetailReadModel?> GetDetailAsync(Guid quotationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Detail ?? AddedQuotations
                .Where(item => item.QuotationId == quotationId)
                .Select(item => new QuotationDetailReadModel
                {
                    QuotationId = item.QuotationId,
                    ProjectId = item.ProjectId,
                    ProposalId = item.ProposalId,
                    QuotationCode = item.QuotationCode,
                    VersionNo = item.VersionNo,
                    SubtotalAmount = item.SubtotalAmount,
                    DiscountAmount = item.DiscountAmount,
                    TaxableAmount = item.TaxableAmount,
                    TaxAmount = item.TaxAmount,
                    TotalAmount = item.TotalAmount,
                    Currency = item.Currency,
                    Status = item.Status,
                    Items = AddedItems.Select(added => new QuotationItemReadModel
                    {
                        QuotationItemId = added.QuotationItemId,
                        QuotationId = added.QuotationId,
                        ItemType = added.ItemType,
                        ProposalItemId = added.ProposalItemId,
                        ProductVersionId = added.ProductVersionId,
                        ItemName = added.ItemName,
                        DisplayOrder = added.DisplayOrder,
                        Quantity = added.Quantity,
                        UnitPrice = added.UnitPrice,
                        CustomizationAdditionalCost = added.CustomizationAdditionalCost,
                        GrossAmount = added.GrossAmount,
                        DiscountAmount = added.DiscountAmount,
                        TaxableAmount = added.TaxableAmount,
                        TaxRate = added.TaxRate,
                        TaxAmount = added.TaxAmount,
                        TotalAmount = added.TotalAmount,
                        SubtotalAmount = added.SubtotalAmount
                    }).ToList()
                })
                .FirstOrDefault());
        }
        public Task<SelectedProposalForQuotationReadModel?> GetSelectedProposalAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(SelectedProposal);
        public Task<bool> HasQuotationForProposalAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult(HasExistingQuotation);
        public Task<IReadOnlyList<ProposalItem>> GetProposalItemsAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProposalItem>>(ProposalItems);
        public Task<IReadOnlyList<QuotationItem>> GetItemsByQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
        {
            var items = AddedItems.Where(item => item.QuotationId == quotationId).ToList();
            return Task.FromResult<IReadOnlyList<QuotationItem>>(items);
        }
        public Task<QuotationItem?> GetItemAsync(Guid quotationItemId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AddedItems.FirstOrDefault(item => item.QuotationItemId == quotationItemId));
        }
        public Task AddItemAsync(QuotationItem item, CancellationToken cancellationToken = default)
        {
            AddedItems.Add(item);
            return Task.CompletedTask;
        }
        public void UpdateItem(QuotationItem item) { }
        public void RemoveItem(QuotationItem item)
        {
            AddedItems.Remove(item);
        }
        public Task AddOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            AddedOrders.Add(order);
            return Task.CompletedTask;
        }
        public Task AddOrderItemAsync(OrderItem item, CancellationToken cancellationToken = default)
        {
            AddedOrderItems.Add(item);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        public List<Order> AddedOrders { get; } = [];
        public List<OrderItem> AddedOrderItems { get; } = [];
        public bool OrderExistsForQuotation { get; set; }

        public IQueryable<Order> Query() => AddedOrders.AsQueryable();
        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(AddedOrders.FirstOrDefault(item => item.OrderId == orderId));
        public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>(AddedOrders);
        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
        {
            AddedOrders.Add(order);
            return Task.CompletedTask;
        }
        public Task AddRangeAsync(IEnumerable<Order> entities, CancellationToken cancellationToken = default)
        {
            AddedOrders.AddRange(entities);
            return Task.CompletedTask;
        }
        public void Update(Order order) { }
        public void Remove(Order order) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Infrastructure.ReadModels.Orders.OrderListItemReadModel>> GetByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Orders.OrderListItemReadModel>>([]);
        public Task<Infrastructure.ReadModels.Orders.OrderDetailReadModel?> GetDetailAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Orders.OrderDetailReadModel?>(null);
        public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
            => Task.FromResult(OrderExistsForQuotation);
        public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default)
        {
            AddedOrderItems.Add(item);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProjectRepository(ProjectDetailReadModel? project, string role) : IProjectRepository
    {
        public Project? ProjectEntity { get; } = project is null
            ? null
            : new Project
            {
                ProjectId = project.ProjectId,
                CustomerId = project.CustomerId,
                AssignedSalesId = project.AssignedSalesId,
                AssignedDesignerId = project.AssignedDesignerId,
                ProjectName = project.ProjectName,
                Status = project.Status
            };

        public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(ProjectEntity?.ProjectId == id ? ProjectEntity : null);
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>([]);
        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Project entity) { }
        public void Remove(Project entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(role);
        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(project);
        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) => Task.FromResult<DesignerAccountReadModel?>(null);
        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);
        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);
        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectSearchIndexItemReadModel?>(null);
        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);
    }

    private sealed class FakeCustomizationRequestRepository(bool hasPending) : ICustomizationRequestRepository
    {
        public IQueryable<CustomizationRequest> Query() => Enumerable.Empty<CustomizationRequest>().AsQueryable();
        public Task<CustomizationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CustomizationRequest?>(null);
        public Task<IReadOnlyList<CustomizationRequest>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomizationRequest>>([]);
        public Task AddAsync(CustomizationRequest entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<CustomizationRequest> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(CustomizationRequest entity) { }
        public void Remove(CustomizationRequest entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<CustomizationRequestReadModel>> GetByProjectAsync(CustomizationRequestQueryReadModel query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomizationRequestReadModel>>([]);
        public Task<CustomizationRequestDetailReadModel?> GetDetailAsync(Guid customizationRequestId, CancellationToken cancellationToken = default) => Task.FromResult<CustomizationRequestDetailReadModel?>(null);
        public Task<CustomizationSubmitContextReadModel?> GetSubmitContextAsync(Guid proposalItemId, CancellationToken cancellationToken = default) => Task.FromResult<CustomizationSubmitContextReadModel?>(null);
        public Task<bool> HasQuotationForProposalAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasProductionVisibleRequestAsync(Guid projectId, Guid productionUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> HasPendingForProposalAsync(Guid proposalId, CancellationToken cancellationToken = default) => Task.FromResult(hasPending);

        public Task<bool> HasActiveRequestForProductVersionAsync(
            Guid projectId,
            Guid proposalId,
            Guid productVersionId,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        public NotificationType? LastType { get; private set; }
        public List<Guid> LastReceiverIds { get; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            LastType = type;
            LastReceiverIds.Clear();
            LastReceiverIds.AddRange(receiverIds);
            return Task.CompletedTask;
        }
    }
}
