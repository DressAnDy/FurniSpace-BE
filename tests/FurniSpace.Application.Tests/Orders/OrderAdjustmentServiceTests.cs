#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Services.Orders;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderAdjustmentServiceTests
{
    private readonly Guid _salesId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _productionId = Guid.NewGuid();

    [Fact]
    public async Task CreateAdjustmentAsync_WhenOrderInProduction_CreatesDraftAdjustment()
    {
        await using var context = CreateContext();
        var seeded = SeedOrderScenario(context, OrderStatus.IN_PRODUCTION);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAdjustmentAsync(
            seeded.OrderId,
            _salesId,
            new CreateOrderAdjustmentDto
            {
                Reason = " One item cannot be produced. ",
                InternalNote = " Production cancelled item. "
            });

        var adjustment = Assert.Single(context.OrderAdjustmentSet);
        Assert.Equal(201, result.Status);
        Assert.Equal("DRAFT", result.Data!.Status);
        Assert.Equal(0m, result.Data.TotalAdjustmentAmount);
        Assert.Equal("One item cannot be produced.", adjustment.Reason);
        Assert.Equal("Production cancelled item.", adjustment.InternalNote);
    }

    [Theory]
    [InlineData(OrderStatus.DEPOSIT_PAID, OrderErrorCodes.OrderNotInProduction)]
    [InlineData(OrderStatus.IN_PRODUCTION, OrderErrorCodes.InvalidAdjustment)]
    public async Task CreateAdjustmentAsync_WhenInvalid_ReturnsBadRequest(
        OrderStatus orderStatus,
        string expectedCode)
    {
        await using var context = CreateContext();
        var seeded = SeedOrderScenario(context, orderStatus);
        await context.SaveChangesAsync();
        var service = BuildService(context);
        var reason = expectedCode == OrderErrorCodes.InvalidAdjustment ? " " : "reason";

        var result = await service.CreateAdjustmentAsync(
            seeded.OrderId,
            _salesId,
            new CreateOrderAdjustmentDto { Reason = reason });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Empty(context.OrderAdjustmentSet);
    }

    [Fact]
    public async Task CreateAdjustmentAsync_WhenMissingUnauthorizedOrForbidden_ReturnsExpectedStatus()
    {
        await using var context = CreateContext();
        var seeded = SeedOrderScenario(context, OrderStatus.IN_PRODUCTION);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.CreateAdjustmentAsync(
            seeded.OrderId,
            Guid.Empty,
            new CreateOrderAdjustmentDto { Reason = "reason" });
        var missing = await service.CreateAdjustmentAsync(
            Guid.NewGuid(),
            _salesId,
            new CreateOrderAdjustmentDto { Reason = "reason" });
        var forbidden = await service.CreateAdjustmentAsync(
            seeded.OrderId,
            _customerId,
            new CreateOrderAdjustmentDto { Reason = "reason" });

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(OrderErrorCodes.OrderNotFound, missing.ErrorCode);
        Assert.Equal(403, forbidden.Status);
    }

    [Fact]
    public async Task AddAdjustmentItemAsync_WhenUnavailableItem_UsesOrderItemSubtotalAndRecalculates()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.AddAdjustmentItemAsync(
            seeded.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.UNAVAILABLE_ITEM,
                OrderItemId = seeded.OrderItemId,
                AdjustmentAmount = 2_000_000m,
                Reason = "Material unavailable."
            });

        Assert.Equal(201, result.Status);
        Assert.Equal("UNAVAILABLE_ITEM", result.Data!.AdjustmentType);
        Assert.Equal(2_000_000m, result.Data.AdjustmentAmount);
        var adjustment = context.OrderAdjustmentSet.Single();
        Assert.Equal(2_000_000m, adjustment.ItemAdjustmentAmount);
        Assert.Equal(2_000_000m, adjustment.TotalAdjustmentAmount);
    }

    [Fact]
    public async Task AddAdjustmentItemAsync_WhenAdditionalDiscount_RecalculatesDiscountTotals()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.AddAdjustmentItemAsync(
            seeded.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                AdjustmentAmount = 500_000m,
                Reason = "Compensation."
            });

        Assert.Equal(201, result.Status);
        Assert.Null(result.Data!.OrderItemId);
        Assert.Equal(500_000m, context.OrderAdjustmentSet.Single().AdditionalDiscountAmount);
    }

    [Fact]
    public async Task AddAdjustmentItemAsync_WhenUnavailableInvalid_ReturnsExpectedErrors()
    {
        await using var context = CreateContext();
        var cancelled = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var wrongAmount = await service.AddAdjustmentItemAsync(
            cancelled.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.UNAVAILABLE_ITEM,
                OrderItemId = cancelled.OrderItemId,
                AdjustmentAmount = 1m,
                Reason = "wrong"
            });
        context.ProductionItemSet.Single().Status = ProductionItemStatus.IN_PRODUCTION;
        await context.SaveChangesAsync();
        var notCancelledResult = await service.AddAdjustmentItemAsync(
            cancelled.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.UNAVAILABLE_ITEM,
                OrderItemId = cancelled.OrderItemId,
                AdjustmentAmount = 2_000_000m,
                Reason = "not cancelled"
            });
        var missingOrderItem = await service.AddAdjustmentItemAsync(
            cancelled.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.UNAVAILABLE_ITEM,
                OrderItemId = Guid.NewGuid(),
                Reason = "missing"
            });

        Assert.Equal(OrderErrorCodes.InvalidUnavailableItemAmount, wrongAmount.ErrorCode);
        Assert.Equal(OrderErrorCodes.ProductionItemNotCancelled, notCancelledResult.ErrorCode);
        Assert.Equal(404, missingOrderItem.Status);
        Assert.Equal(OrderErrorCodes.OrderItemNotFound, missingOrderItem.ErrorCode);
    }

    [Fact]
    public async Task AddAdjustmentItemAsync_WhenInvalidOrConfirmed_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var adjustment = await context.OrderAdjustmentSet.FindAsync(seeded.AdjustmentId);
        adjustment!.Status = OrderAdjustmentStatus.CONFIRMED;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var confirmed = await service.AddAdjustmentItemAsync(
            seeded.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                AdjustmentAmount = 500_000m,
                Reason = "confirmed"
            });
        adjustment.Status = OrderAdjustmentStatus.DRAFT;
        await context.SaveChangesAsync();
        var invalid = await service.AddAdjustmentItemAsync(
            seeded.AdjustmentId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                AdjustmentAmount = 0m,
                Reason = "invalid"
            });
        var missingAdjustment = await service.AddAdjustmentItemAsync(
            Guid.NewGuid(),
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                AdjustmentAmount = 1m,
                Reason = "missing"
            });

        Assert.Equal(OrderErrorCodes.AdjustmentAlreadyConfirmed, confirmed.ErrorCode);
        Assert.Equal(OrderErrorCodes.InvalidAdjustmentItem, invalid.ErrorCode);
        Assert.Equal(404, missingAdjustment.Status);
        Assert.Equal(OrderErrorCodes.OrderAdjustmentNotFound, missingAdjustment.ErrorCode);
    }

    [Fact]
    public async Task UpdateAdjustmentItemAsync_UpdatesItemAndRecalculatesTotals()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        var item = CreateAdjustmentItem(seeded.AdjustmentId, OrderAdjustmentItemType.ADDITIONAL_DISCOUNT, 100_000m);
        context.OrderAdjustmentItemSet.Add(item);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.UpdateAdjustmentItemAsync(
            item.OrderAdjustmentItemId,
            _salesId,
            new UpsertOrderAdjustmentItemDto
            {
                AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
                AdjustmentAmount = 750_000m,
                Reason = "Updated compensation."
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(750_000m, result.Data!.AdjustmentAmount);
        Assert.Equal("Updated compensation.", context.OrderAdjustmentItemSet.Single().Reason);
        Assert.Equal(750_000m, context.OrderAdjustmentSet.Single().AdditionalDiscountAmount);
    }

    [Fact]
    public async Task DeleteAdjustmentItemAsync_RemovesItemAndRecalculatesTotals()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var item = CreateAdjustmentItem(seeded.AdjustmentId, OrderAdjustmentItemType.ADDITIONAL_DISCOUNT, 250_000m);
        context.OrderAdjustmentItemSet.Add(item);
        var adjustment = await context.OrderAdjustmentSet.FindAsync(seeded.AdjustmentId);
        adjustment!.AdditionalDiscountAmount = 250_000m;
        adjustment.TotalAdjustmentAmount = 250_000m;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.DeleteAdjustmentItemAsync(item.OrderAdjustmentItemId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Empty(context.OrderAdjustmentItemSet);
        Assert.Equal(0m, result.Data!.TotalAdjustmentAmount);
    }

    [Fact]
    public async Task ConfirmAdjustmentAsync_WhenDraftWithItems_ConfirmsAdjustment()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        context.OrderAdjustmentItemSet.Add(CreateAdjustmentItem(
            seeded.AdjustmentId,
            OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
            250_000m));
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.ConfirmAdjustmentAsync(seeded.AdjustmentId, _customerId);

        var adjustment = context.OrderAdjustmentSet.Single();
        Assert.Equal(200, result.Status);
        Assert.Equal("CONFIRMED", result.Data!.Status);
        Assert.Equal(_customerId, result.Data.ConfirmedBy);
        Assert.Equal(OrderAdjustmentStatus.CONFIRMED, adjustment.Status);
        Assert.NotNull(adjustment.ConfirmedAt);
    }

    [Fact]
    public async Task ConfirmAdjustmentAsync_WhenNoItems_ReturnsAdjustmentItemRequired()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.ConfirmAdjustmentAsync(seeded.AdjustmentId, _customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.AdjustmentItemRequired, result.ErrorCode);
        Assert.Equal(OrderAdjustmentStatus.DRAFT, context.OrderAdjustmentSet.Single().Status);
    }

    [Fact]
    public async Task ConfirmAdjustmentAsync_WhenAlreadyConfirmed_IsIdempotent()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        var confirmedAt = DateTime.UtcNow.AddMinutes(-5);
        var adjustment = context.OrderAdjustmentSet.Local.Single();
        adjustment.Status = OrderAdjustmentStatus.CONFIRMED;
        adjustment.ConfirmedBy = _customerId;
        adjustment.ConfirmedAt = confirmedAt;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.ConfirmAdjustmentAsync(seeded.AdjustmentId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal("CONFIRMED", result.Data!.Status);
        Assert.Equal(confirmedAt, result.Data.ConfirmedAt);
    }

    [Fact]
    public async Task ConfirmAdjustmentAsync_WhenInvalid_ReturnsExpectedErrors()
    {
        await using var context = CreateContext();
        var seeded = SeedAdjustmentItemScenario(context, ProductionItemStatus.CANCELLED);
        var adjustment = context.OrderAdjustmentSet.Local.Single();
        adjustment.Status = OrderAdjustmentStatus.APPLIED;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.ConfirmAdjustmentAsync(seeded.AdjustmentId, Guid.Empty);
        var forbidden = await service.ConfirmAdjustmentAsync(seeded.AdjustmentId, _salesId);
        var missing = await service.ConfirmAdjustmentAsync(Guid.NewGuid(), _customerId);
        var invalidStatus = await service.ConfirmAdjustmentAsync(seeded.AdjustmentId, _customerId);

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(OrderErrorCodes.OrderAdjustmentNotFound, missing.ErrorCode);
        Assert.Equal(400, invalidStatus.Status);
        Assert.Equal(OrderErrorCodes.InvalidAdjustmentStatus, invalidStatus.ErrorCode);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenReadyAndScheduleConfirmed_MovesOrderAndProjectToDelivering()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.READY_FOR_DELIVERY, ProjectStatus.READY_FOR_DELIVERY);
        AddDeliverySchedule(context, seeded.ProjectId, ProjectScheduleStatus.CONFIRMED);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.StartDeliveryAsync(seeded.OrderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal("DELIVERING", result.Data!.OrderStatus);
        Assert.Equal("DELIVERING", result.Data.ProjectStatus);
        Assert.Equal(OrderStatus.DELIVERING, context.OrderSet.Single().Status);
        Assert.Equal(ProjectStatus.DELIVERING, context.ProjectSet.Single().Status);
    }

    [Fact]
    public async Task StartDeliveryAsync_ProductionCanStartDelivery()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.READY_FOR_DELIVERY, ProjectStatus.READY_FOR_DELIVERY);
        AddDeliverySchedule(context, seeded.ProjectId, ProjectScheduleStatus.CONFIRMED);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.StartDeliveryAsync(seeded.OrderId, _productionId);

        Assert.Equal(200, result.Status);
        Assert.Equal("DELIVERING", result.Data!.OrderStatus);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenAlreadyDelivering_IsIdempotent()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.DELIVERING, ProjectStatus.DELIVERING);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.StartDeliveryAsync(seeded.OrderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal("DELIVERING", result.Data!.OrderStatus);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenNoConfirmedDeliverySchedule_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.READY_FOR_DELIVERY, ProjectStatus.READY_FOR_DELIVERY);
        AddDeliverySchedule(context, seeded.ProjectId, ProjectScheduleStatus.PENDING_CONFIRMATION);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.StartDeliveryAsync(seeded.OrderId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryScheduleNotConfirmed, result.ErrorCode);
        Assert.Equal(OrderStatus.READY_FOR_DELIVERY, context.OrderSet.Single().Status);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenInvalid_ReturnsExpectedErrors()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.IN_PRODUCTION, ProjectStatus.IN_PRODUCTION);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.StartDeliveryAsync(seeded.OrderId, Guid.Empty);
        var forbidden = await service.StartDeliveryAsync(seeded.OrderId, _customerId);
        var missing = await service.StartDeliveryAsync(Guid.NewGuid(), _salesId);
        var invalidStatus = await service.StartDeliveryAsync(seeded.OrderId, _salesId);

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(OrderErrorCodes.OrderNotFound, missing.ErrorCode);
        Assert.Equal(400, invalidStatus.Status);
        Assert.Equal(OrderErrorCodes.InvalidOrderStatus, invalidStatus.ErrorCode);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenProjectMissing_ReturnsProjectNotFound()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.READY_FOR_DELIVERY, ProjectStatus.READY_FOR_DELIVERY);
        context.ProjectSet.Remove(context.ProjectSet.Local.Single(project => project.ProjectId == seeded.ProjectId));
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.StartDeliveryAsync(seeded.OrderId, _adminId);

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateDeliveredQuantityAsync_WhenValid_IncrementsDeliveredQuantity()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.DELIVERING, ProjectStatus.DELIVERING);
        var item = AddOrderItem(context, seeded.OrderId, quantity: 4, deliveredQuantity: 2);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.UpdateDeliveredQuantityAsync(
            item.OrderItemId,
            _productionId,
            new UpdateDeliveredQuantityRequestDto
            {
                DeliveredQuantityIncrement = 2,
                DeliveryNote = " Delivered two chairs. "
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(4, result.Data!.Quantity);
        Assert.Equal(4, result.Data.DeliveredQuantity);
        Assert.Equal(4, item.DeliveredQuantity);
        Assert.Equal("Delivered two chairs.", item.DeliveryNote);
        Assert.Equal(_productionId, item.LastDeliveredBy);
        Assert.NotNull(item.LastDeliveredAt);
    }

    [Theory]
    [InlineData(0, OrderErrorCodes.InvalidDeliveredQuantity)]
    [InlineData(3, OrderErrorCodes.DeliveredQuantityExceeded)]
    public async Task UpdateDeliveredQuantityAsync_WhenQuantityInvalid_ReturnsBadRequest(
        int increment,
        string expectedCode)
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.DELIVERING, ProjectStatus.DELIVERING);
        var item = AddOrderItem(context, seeded.OrderId, quantity: 4, deliveredQuantity: 2);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.UpdateDeliveredQuantityAsync(
            item.OrderItemId,
            _salesId,
            new UpdateDeliveredQuantityRequestDto { DeliveredQuantityIncrement = increment });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
    }

    [Theory]
    [InlineData(QuotationItemType.MANUAL_ITEM, OrderItemStatus.PENDING, OrderErrorCodes.ItemNotDeliverable)]
    [InlineData(QuotationItemType.PRODUCT_ITEM, OrderItemStatus.CANCELLED, OrderErrorCodes.ItemNotDeliverable)]
    [InlineData(QuotationItemType.PRODUCT_ITEM, OrderItemStatus.PENDING, OrderErrorCodes.OrderNotDelivering)]
    public async Task UpdateDeliveredQuantityAsync_WhenNotDeliverable_ReturnsExpectedError(
        QuotationItemType itemType,
        OrderItemStatus itemStatus,
        string expectedCode)
    {
        await using var context = CreateContext();
        var orderStatus = expectedCode == OrderErrorCodes.OrderNotDelivering
            ? OrderStatus.READY_FOR_DELIVERY
            : OrderStatus.DELIVERING;
        var seeded = SeedDeliveryScenario(context, orderStatus, ProjectStatus.DELIVERING);
        var item = AddOrderItem(context, seeded.OrderId, itemType, itemStatus);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.UpdateDeliveredQuantityAsync(
            item.OrderItemId,
            _salesId,
            new UpdateDeliveredQuantityRequestDto { DeliveredQuantityIncrement = 1 });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateDeliveredQuantityAsync_WhenMissingUnauthorizedOrForbidden_ReturnsExpectedStatus()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.DELIVERING, ProjectStatus.DELIVERING);
        var item = AddOrderItem(context, seeded.OrderId);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.UpdateDeliveredQuantityAsync(
            item.OrderItemId,
            Guid.Empty,
            new UpdateDeliveredQuantityRequestDto { DeliveredQuantityIncrement = 1 });
        var forbidden = await service.UpdateDeliveredQuantityAsync(
            item.OrderItemId,
            _customerId,
            new UpdateDeliveredQuantityRequestDto { DeliveredQuantityIncrement = 1 });
        var missing = await service.UpdateDeliveredQuantityAsync(
            Guid.NewGuid(),
            _salesId,
            new UpdateDeliveredQuantityRequestDto { DeliveredQuantityIncrement = 1 });

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(OrderErrorCodes.OrderItemNotFound, missing.ErrorCode);
    }

    [Fact]
    public async Task ConfirmItemDeliveryAsync_WhenFullyDelivered_ConfirmsItem()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.DELIVERING, ProjectStatus.DELIVERING);
        var item = AddOrderItem(context, seeded.OrderId, quantity: 4, deliveredQuantity: 4);
        AddOrderItem(context, seeded.OrderId, quantity: 1, deliveredQuantity: 0);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.ConfirmItemDeliveryAsync(item.OrderItemId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal("DELIVERED", result.Data!.Status);
        Assert.Equal("DELIVERING", result.Data.OrderStatus);
        Assert.Equal(OrderItemStatus.DELIVERED, item.Status);
        Assert.NotNull(item.CustomerConfirmedAt);
        Assert.Equal(OrderStatus.DELIVERING, context.OrderSet.Single().Status);
    }

    [Fact]
    public async Task ConfirmItemDeliveryAsync_WhenFinalDeliverableItem_CompletesOrderAndProjectDelivery()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.DELIVERING, ProjectStatus.DELIVERING);
        var finalItem = AddOrderItem(context, seeded.OrderId, quantity: 2, deliveredQuantity: 2);
        AddOrderItem(context, seeded.OrderId, status: OrderItemStatus.DELIVERED);
        AddOrderItem(context, seeded.OrderId, QuotationItemType.MANUAL_ITEM);
        AddOrderItem(context, seeded.OrderId, status: OrderItemStatus.UNAVAILABLE);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.ConfirmItemDeliveryAsync(finalItem.OrderItemId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal("DELIVERED", result.Data!.OrderStatus);
        Assert.Equal(OrderStatus.DELIVERED, context.OrderSet.Single().Status);
        Assert.Equal(ProjectStatus.DELIVERED, context.ProjectSet.Single().Status);
        Assert.NotNull(context.OrderSet.Single().CustomerConfirmedDeliveryAt);
    }

    [Fact]
    public async Task ConfirmItemDeliveryAsync_WhenAlreadyDelivered_IsIdempotent()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.DELIVERING, ProjectStatus.DELIVERING);
        var item = AddOrderItem(context, seeded.OrderId, status: OrderItemStatus.DELIVERED);
        item.CustomerConfirmedAt = DateTime.UtcNow.AddMinutes(-5);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.ConfirmItemDeliveryAsync(item.OrderItemId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal("DELIVERED", result.Data!.Status);
        Assert.Equal(item.CustomerConfirmedAt, result.Data.CustomerConfirmedAt);
    }

    [Theory]
    [InlineData(QuotationItemType.PRODUCT_ITEM, OrderItemStatus.PENDING, 2, 1, OrderErrorCodes.ItemNotFullyDelivered)]
    [InlineData(QuotationItemType.MANUAL_ITEM, OrderItemStatus.PENDING, 1, 1, OrderErrorCodes.ItemNotDeliverable)]
    public async Task ConfirmItemDeliveryAsync_WhenInvalid_ReturnsExpectedBadRequest(
        QuotationItemType itemType,
        OrderItemStatus status,
        int quantity,
        int deliveredQuantity,
        string expectedCode)
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.DELIVERING, ProjectStatus.DELIVERING);
        var item = AddOrderItem(context, seeded.OrderId, itemType, status, quantity, deliveredQuantity);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.ConfirmItemDeliveryAsync(item.OrderItemId, _customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmItemDeliveryAsync_WhenMissingUnauthorizedOrForbidden_ReturnsExpectedStatus()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.READY_FOR_DELIVERY, ProjectStatus.READY_FOR_DELIVERY);
        var item = AddOrderItem(context, seeded.OrderId, quantity: 1, deliveredQuantity: 1);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.ConfirmItemDeliveryAsync(item.OrderItemId, Guid.Empty);
        var forbidden = await service.ConfirmItemDeliveryAsync(item.OrderItemId, _salesId);
        var missing = await service.ConfirmItemDeliveryAsync(Guid.NewGuid(), _customerId);
        var orderNotDelivering = await service.ConfirmItemDeliveryAsync(item.OrderItemId, _customerId);

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(OrderErrorCodes.OrderItemNotFound, missing.ErrorCode);
        Assert.Equal(400, orderNotDelivering.Status);
        Assert.Equal(OrderErrorCodes.OrderNotDelivering, orderNotDelivering.ErrorCode);
    }

    [Fact]
    public async Task PrepareFinalPaymentAsync_WhenRemainingPositive_MovesToFinalPaymentPending()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        AddPaidPayment(context, seeded.OrderId, amount: 10m);
        context.OrderAdjustmentSet.Add(CreateAppliedAdjustment(seeded.OrderId, itemAdjustment: 0m, discount: 5m));
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.PrepareFinalPaymentAsync(seeded.OrderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.True(result.Data!.RequiresRemainingPayment);
        Assert.Equal("FINAL_PAYMENT_PENDING", result.Data.Status);
        Assert.Equal(95m, result.Data.FinalTotalAmount);
        Assert.Equal(10m, result.Data.PaidAmount);
        Assert.Equal(85m, result.Data.RemainingAmount);
        Assert.Equal(OrderStatus.FINAL_PAYMENT_PENDING, context.OrderSet.Single().Status);
    }

    [Fact]
    public async Task PrepareFinalPaymentAsync_WhenRemainingZero_KeepsDelivered()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        AddPaidPayment(context, seeded.OrderId, amount: 100m);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.PrepareFinalPaymentAsync(seeded.OrderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.False(result.Data!.RequiresRemainingPayment);
        Assert.Equal("DELIVERED", result.Data.Status);
        Assert.Equal(0m, result.Data.RemainingAmount);
        Assert.Equal(OrderStatus.DELIVERED, context.OrderSet.Single().Status);
    }

    [Fact]
    public async Task PrepareFinalPaymentAsync_WhenAdjustmentConfirmed_ReturnsAdjustmentNotApplied()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        context.OrderAdjustmentSet.Add(CreateAppliedAdjustment(
            seeded.OrderId,
            itemAdjustment: 0m,
            discount: 5m,
            status: OrderAdjustmentStatus.CONFIRMED));
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.PrepareFinalPaymentAsync(seeded.OrderId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.AdjustmentNotApplied, result.ErrorCode);
    }

    [Fact]
    public async Task PrepareFinalPaymentAsync_WhenInvalid_ReturnsExpectedErrors()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveryScenario(context, OrderStatus.DELIVERING, ProjectStatus.DELIVERING);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.PrepareFinalPaymentAsync(seeded.OrderId, Guid.Empty);
        var forbidden = await service.PrepareFinalPaymentAsync(seeded.OrderId, _customerId);
        var missing = await service.PrepareFinalPaymentAsync(Guid.NewGuid(), _salesId);
        var orderNotDelivered = await service.PrepareFinalPaymentAsync(seeded.OrderId, _salesId);

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(OrderErrorCodes.OrderNotFound, missing.ErrorCode);
        Assert.Equal(400, orderNotDelivered.Status);
        Assert.Equal(OrderErrorCodes.OrderNotDelivered, orderNotDelivered.ErrorCode);
    }

    [Fact]
    public async Task PrepareFinalPaymentAsync_WhenDeliveryNotConfirmed_ReturnsDeliveryNotConfirmed()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        context.OrderSet.Local.Single(order => order.OrderId == seeded.OrderId).CustomerConfirmedDeliveryAt = null;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.PrepareFinalPaymentAsync(seeded.OrderId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryNotConfirmed, result.ErrorCode);
    }

    [Fact]
    public async Task PrepareFinalPaymentAsync_WhenPaidExceedsFinalTotal_ReturnsNegativeRemainingAmount()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        AddPaidPayment(context, seeded.OrderId, amount: 120m);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.PrepareFinalPaymentAsync(seeded.OrderId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.NegativeRemainingAmount, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenReady_CompletesOrderAndProject()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        var item = AddOrderItem(context, seeded.OrderId, status: OrderItemStatus.DELIVERED);
        item.DeliveredQuantity = item.Quantity;
        context.OrderSet.Local.Single(order => order.OrderId == seeded.OrderId).Status = OrderStatus.FINAL_PAYMENT_PENDING;
        AddPaidPayment(context, seeded.OrderId, amount: 100m);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CompleteAsync(seeded.OrderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal("COMPLETED", result.Data!.OrderStatus);
        Assert.Equal("COMPLETED", result.Data.ProjectStatus);
        Assert.Equal(OrderStatus.COMPLETED, context.OrderSet.Single().Status);
        Assert.Equal(ProjectStatus.COMPLETED, context.ProjectSet.Single().Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenAlreadyCompleted_ReturnsSuccess()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        var completedAt = DateTime.UtcNow.AddMinutes(-3);
        var order = context.OrderSet.Local.Single(order => order.OrderId == seeded.OrderId);
        order.Status = OrderStatus.COMPLETED;
        order.UpdatedAt = completedAt;
        context.ProjectSet.Local.Single(project => project.ProjectId == seeded.ProjectId).Status = ProjectStatus.COMPLETED;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CompleteAsync(seeded.OrderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal("COMPLETED", result.Data!.OrderStatus);
        Assert.Equal(OrderStatus.COMPLETED, context.OrderSet.Single().Status);
        Assert.Equal(ProjectStatus.COMPLETED, context.ProjectSet.Single().Status);
        Assert.True(result.Data.CompletedAt >= completedAt);
    }

    [Theory]
    [InlineData(OrderStatus.READY_FOR_DELIVERY, OrderErrorCodes.OrderNotReadyToComplete)]
    [InlineData(OrderStatus.DELIVERED, OrderErrorCodes.DeliveryNotCompleted)]
    public async Task CompleteAsync_WhenOrderNotReady_ReturnsExpectedError(
        OrderStatus status,
        string expectedCode)
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        var order = context.OrderSet.Local.Single(order => order.OrderId == seeded.OrderId);
        order.Status = status;
        if (expectedCode == OrderErrorCodes.DeliveryNotCompleted)
        {
            order.CustomerConfirmedDeliveryAt = null;
        }

        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CompleteAsync(seeded.OrderId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenDeliverableItemNotConfirmed_ReturnsDeliveryNotCompleted()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        AddOrderItem(context, seeded.OrderId, status: OrderItemStatus.READY, quantity: 1, deliveredQuantity: 1);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CompleteAsync(seeded.OrderId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryNotCompleted, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenAdjustmentNotApplied_ReturnsAdjustmentNotApplied()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        context.OrderAdjustmentSet.Add(CreateAppliedAdjustment(
            seeded.OrderId,
            itemAdjustment: 0m,
            discount: 5m,
            status: OrderAdjustmentStatus.CONFIRMED));
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CompleteAsync(seeded.OrderId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.AdjustmentNotApplied, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenRemainingPaymentNotPaid_ReturnsRemainingPaymentNotPaid()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        context.OrderSet.Local.Single(order => order.OrderId == seeded.OrderId).Status = OrderStatus.FINAL_PAYMENT_PENDING;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CompleteAsync(seeded.OrderId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.RemainingPaymentNotPaid, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenMissingUnauthorizedOrForbidden_ReturnsExpectedStatus()
    {
        await using var context = CreateContext();
        var seeded = SeedDeliveredOrderScenario(context);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.CompleteAsync(seeded.OrderId, Guid.Empty);
        var forbidden = await service.CompleteAsync(seeded.OrderId, _customerId);
        var missing = await service.CompleteAsync(Guid.NewGuid(), _salesId);

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(OrderErrorCodes.OrderNotFound, missing.ErrorCode);
    }

    private static OrderService BuildService(AppDbContext context)
    {
        return new OrderService(
            new OrderRepository(context),
            new ProjectRepository(context),
            new PaymentRepository(context),
            new ProjectScheduleRepository(context),
            new InMemoryUnitOfWork(context));
    }

    private SeededOrder SeedOrderScenario(AppDbContext context, OrderStatus orderStatus)
    {
        var salesRole = CreateRole("SALES");
        var adminRole = CreateRole("ADMIN");
        var customerRole = CreateRole("CUSTOMER");
        var productionRole = CreateRole("PRODUCTION");
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        context.RoleSet.AddRange(salesRole, adminRole, customerRole, productionRole);
        context.AccountSet.AddRange(
            CreateAccount(_salesId, salesRole.RoleId, "sales@example.com"),
            CreateAccount(_adminId, adminRole.RoleId, "admin@example.com"),
            CreateAccount(_productionId, productionRole.RoleId, "production@example.com"),
            CreateAccount(_customerId, customerRole.RoleId, "customer@example.com"));
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            ProjectName = "Cafe",
            Status = ProjectStatus.IN_PRODUCTION
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-001",
            CustomerId = _customerId,
            SalesId = _salesId,
            OriginalTotalAmount = 5_000_000m,
            FinalTotalAmount = 5_000_000m,
            Status = orderStatus
        });
        return new SeededOrder(orderId, projectId);
    }

    private SeededOrder SeedDeliveryScenario(
        AppDbContext context,
        OrderStatus orderStatus,
        ProjectStatus projectStatus)
    {
        var seeded = SeedOrderScenario(context, orderStatus);
        context.ProjectSet.Local.Single(project => project.ProjectId == seeded.ProjectId).Status = projectStatus;
        return seeded;
    }

    private SeededOrder SeedDeliveredOrderScenario(AppDbContext context)
    {
        var seeded = SeedDeliveryScenario(context, OrderStatus.DELIVERED, ProjectStatus.DELIVERED);
        var order = context.OrderSet.Local.Single(order => order.OrderId == seeded.OrderId);
        order.CustomerConfirmedDeliveryAt = DateTime.UtcNow.AddMinutes(-10);
        order.OriginalTotalAmount = 100m;
        order.FinalTotalAmount = 100m;
        order.PaidAmount = 0m;
        order.RemainingAmount = 100m;
        return seeded;
    }

    private static void AddPaidPayment(AppDbContext context, Guid orderId, decimal amount)
    {
        context.PaymentSet.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = context.OrderSet.Local.Single(order => order.OrderId == orderId).ProjectId,
            OrderId = orderId,
            PaymentCode = $"PAY-{Guid.NewGuid():N}"[..20],
            PaymentType = PaymentType.DEPOSIT,
            Amount = amount,
            Currency = "VND",
            Status = PaymentStatus.PAID,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static OrderAdjustment CreateAppliedAdjustment(
        Guid orderId,
        decimal itemAdjustment,
        decimal discount,
        OrderAdjustmentStatus status = OrderAdjustmentStatus.APPLIED)
    {
        return new OrderAdjustment
        {
            OrderAdjustmentId = Guid.NewGuid(),
            OrderId = orderId,
            Status = status,
            ItemAdjustmentAmount = itemAdjustment,
            AdditionalDiscountAmount = discount,
            TotalAdjustmentAmount = itemAdjustment + discount,
            Reason = "Final adjustment",
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static void AddDeliverySchedule(
        AppDbContext context,
        Guid projectId,
        ProjectScheduleStatus status)
    {
        context.ProjectScheduleSet.Add(new ProjectSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = projectId,
            ScheduleType = ProjectScheduleType.DELIVERY,
            Status = status,
            Title = "Delivery",
            ScheduledStart = DateTime.UtcNow.AddDays(1)
        });
    }

    private static OrderItem AddOrderItem(
        AppDbContext context,
        Guid orderId,
        QuotationItemType itemType = QuotationItemType.PRODUCT_ITEM,
        OrderItemStatus status = OrderItemStatus.PENDING,
        int quantity = 4,
        int deliveredQuantity = 0)
    {
        var item = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ItemType = itemType,
            ProductNameSnapshot = "Chair",
            Quantity = quantity,
            DeliveredQuantity = deliveredQuantity,
            Status = status
        };
        context.OrderItemSet.Add(item);
        return item;
    }

    private SeededAdjustmentItem SeedAdjustmentItemScenario(
        AppDbContext context,
        ProductionItemStatus productionItemStatus)
    {
        var order = SeedOrderScenario(context, OrderStatus.IN_PRODUCTION);
        var orderItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = order.OrderId,
            ItemType = QuotationItemType.PRODUCT_ITEM,
            ProductNameSnapshot = "Cabinet",
            Quantity = 1,
            SubtotalAmount = 2_000_000m,
            Status = OrderItemStatus.PENDING
        };
        var productionRequest = new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProjectId = order.ProjectId,
            OrderId = order.OrderId,
            Status = ProductionRequestStatus.IN_PRODUCTION
        };
        var productionItem = new ProductionItem
        {
            ProductionItemId = Guid.NewGuid(),
            ProductionRequestId = productionRequest.ProductionRequestId,
            OrderItemId = orderItem.OrderItemId,
            Status = productionItemStatus
        };
        var adjustment = new OrderAdjustment
        {
            OrderAdjustmentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Status = OrderAdjustmentStatus.DRAFT,
            Reason = "Adjustment",
            CreatedBy = _salesId,
            CreatedAt = DateTime.UtcNow
        };
        context.OrderItemSet.Add(orderItem);
        context.ProductionRequestSet.Add(productionRequest);
        context.ProductionItemSet.Add(productionItem);
        context.OrderAdjustmentSet.Add(adjustment);
        return new SeededAdjustmentItem(adjustment.OrderAdjustmentId, orderItem.OrderItemId);
    }

    private OrderAdjustmentItem CreateAdjustmentItem(
        Guid adjustmentId,
        OrderAdjustmentItemType type,
        decimal amount)
    {
        return new OrderAdjustmentItem
        {
            OrderAdjustmentItemId = Guid.NewGuid(),
            OrderAdjustmentId = adjustmentId,
            AdjustmentType = type,
            AdjustmentAmount = amount,
            Reason = "Existing",
            CreatedBy = _salesId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Role CreateRole(string roleName)
    {
        return new Role
        {
            RoleId = Guid.NewGuid(),
            RoleName = roleName,
            Description = $"{roleName} role"
        };
    }

    private static Account CreateAccount(Guid accountId, Guid roleId, string email)
    {
        return new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = email,
            Status = AccountStatus.ACTIVE
        };
    }

    private sealed class InMemoryUnitOfWork(AppDbContext context) : IUnitOfWork
    {
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => context.SaveChangesAsync(cancellationToken);

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed record SeededOrder(Guid OrderId, Guid ProjectId);

    private sealed record SeededAdjustmentItem(Guid AdjustmentId, Guid OrderItemId);
}
