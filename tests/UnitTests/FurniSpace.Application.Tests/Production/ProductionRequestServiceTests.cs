#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Application.Services.Production;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Application.Tests.Production;

public sealed class ProductionRequestServiceTests
{
    private readonly Guid _salesId = Guid.NewGuid();
    private readonly Guid _productionId = Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_WhenValid_CreatesRequestItemsUpdatesWorkflowAndNotifies()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        context.OrderItemSet.AddRange(
            CreateOrderItem(data.OrderId, true, "Counter"),
            CreateOrderItem(data.OrderId, false, "Shipping"));
        await context.SaveChangesAsync();
        var dispatcher = new CapturingNotificationDispatcher();
        var service = BuildService(context, dispatcher);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto
            {
                AssignedTo = _productionId,
                Priority = " normal ",
                EstimatedStartDate = new DateOnly(2026, 7, 25),
                EstimatedCompletionDate = new DateOnly(2026, 8, 10),
                Note = " Start soon "
            });

        Assert.Equal(201, result.Status);
        Assert.Equal(data.OrderId, result.Data!.OrderId);
        Assert.Equal(data.ProjectId, result.Data.ProjectId);
        Assert.Equal(_productionId, result.Data.AssignedTo);
        Assert.Equal("PENDING", result.Data.Status);
        Assert.Equal(2, result.Data.ProductionItemCount);
        Assert.StartsWith("PRD-", result.Data.ProductionCode, StringComparison.Ordinal);
        Assert.Equal(OrderStatus.IN_PRODUCTION, context.OrderSet.Single().Status);
        Assert.Equal(ProjectStatus.IN_PRODUCTION, context.ProjectSet.Single().Status);
        Assert.Equal(2, context.ProductionItemSet.Count());
        Assert.All(context.OrderItemSet, item => Assert.Equal(OrderItemStatus.IN_PRODUCTION, item.Status));
        Assert.Equal("NORMAL", context.ProductionRequestSet.Single().Priority);
        Assert.Equal("Start soon", context.ProductionRequestSet.Single().Note);
        Assert.Equal(2, dispatcher.Dispatches.Count);
        Assert.Contains(
            dispatcher.Dispatches,
            dispatch => dispatch.Type == NotificationType.ProductionRequestCreated &&
                        dispatch.Receivers.Contains(_salesId) &&
                        dispatch.Receivers.Contains(_productionId));
        Assert.Contains(
            dispatcher.Dispatches,
            dispatch => dispatch.Type == NotificationType.ProductionRequestAssigned &&
                        dispatch.Receivers.Contains(_salesId) &&
                        dispatch.Receivers.Contains(_productionId));
    }

    [Theory]
    [InlineData(OrderStatus.CREATED, PaymentStatus.PAID, ProductionErrorCodes.InvalidOrderStatus)]
    [InlineData(OrderStatus.DEPOSIT_PAID, PaymentStatus.PENDING, ProductionErrorCodes.DepositNotPaid)]
    public async Task CreateAsync_WhenOrderOrDepositInvalid_ReturnsBadRequest(
        OrderStatus orderStatus,
        PaymentStatus paymentStatus,
        string expectedCode)
    {
        await using var context = CreateContext();
        var data = SeedBase(context, orderStatus, paymentStatus);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Empty(context.ProductionRequestSet);
    }

    [Fact]
    public async Task CreateAsync_WhenProductionDeadlineMissing_ReturnsRequiredError()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        context.OrderItemSet.Add(CreateOrderItem(data.OrderId, true, "Counter"));
        await context.SaveChangesAsync();
        var service = BuildService(
            context,
            phaseDeadlines: new CapturingProjectPhaseDeadlineService { HasProductionDeadline = false });

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectPhaseDeadlineErrorCodes.ProductionDeadlineRequired, result.ErrorCode);
        Assert.Empty(context.ProductionRequestSet);
    }

    [Fact]
    public async Task CreateAsync_WhenEstimatedDatesExceedTarget_ReturnsValidationError()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        await context.SaveChangesAsync();
        var project = context.ProjectSet.Single();
        project.TargetCompletionDate = new DateOnly(2026, 8, 1);
        context.OrderItemSet.Add(CreateOrderItem(data.OrderId, true, "Counter"));
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto
            {
                AssignedTo = _productionId,
                EstimatedCompletionDate = new DateOnly(2026, 9, 1)
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductionErrorCodes.ProductionDateExceedsTarget, result.ErrorCode);
        Assert.Empty(context.ProductionRequestSet);
    }

    [Fact]
    public async Task StartAsync_WhenActualStartExceedsTarget_AllowsOverdueProductionStart()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        await context.SaveChangesAsync();
        var project = context.ProjectSet.Single();
        project.TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var productionRequest = CreateProductionRequest(
            data.ProjectId,
            data.OrderId,
            _productionId,
            ProductionRequestStatus.PENDING);
        context.ProductionRequestSet.Add(productionRequest);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.StartAsync(
            productionRequest.ProductionRequestId,
            _productionId,
            new StartProductionRequestDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(ProductionRequestStatus.IN_PRODUCTION, context.ProductionRequestSet.Single().Status);
        Assert.NotNull(context.ProductionRequestSet.Single().ActualStartDate);
    }

    [Fact]
    public async Task CreateAsync_WhenActiveRequestExists_ReturnsConflict()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        context.ProductionRequestSet.Add(new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProjectId = data.ProjectId,
            OrderId = data.OrderId,
            AssignedTo = _productionId,
            Status = ProductionRequestStatus.PENDING
        });
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(409, result.Status);
        Assert.Equal(ProductionErrorCodes.ProductionRequestAlreadyExists, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenNoOrderItems_ReturnsOrderItemNotEligible()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductionErrorCodes.OrderItemNotEligibleForProduction, result.ErrorCode);
        Assert.Empty(context.ProductionRequestSet);
    }

    [Fact]
    public async Task CreateAsync_WhenProductItemStatusInvalid_ReturnsInvalidOrderItemTransition()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        var item = CreateOrderItem(data.OrderId, true, "Chair");
        item.Status = OrderItemStatus.READY;
        context.OrderItemSet.Add(item);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderItemStatusTransitionService.InvalidTransitionCode, result.ErrorCode);
        Assert.Empty(context.ProductionRequestSet);
        Assert.Equal(OrderItemStatus.READY, context.OrderItemSet.Single().Status);
    }

    [Fact]
    public async Task CreateAsync_WhenProductionStaffInvalid_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = Guid.NewGuid() });

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductionErrorCodes.ProductionStaffNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenOrderMissing_ReturnsNotFound()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            Guid.NewGuid(),
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductionErrorCodes.OrderNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenProjectMissing_ReturnsNotFound()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        var orderId = Guid.NewGuid();
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = Guid.NewGuid(),
            QuotationId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            SalesId = _salesId,
            OrderCode = "ORD-MISSING-PROJECT",
            Status = OrderStatus.DEPOSIT_PAID
        });
        context.PaymentSet.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            OrderId = orderId,
            PaymentCode = "PAY-MISSING-PROJECT",
            PaymentType = PaymentType.DEPOSIT,
            Status = PaymentStatus.PAID,
            Amount = 100m
        });
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            orderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(404, result.Status);
        Assert.Equal(ProductionErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenNotificationFails_DoesNotRollbackCreation()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        context.OrderItemSet.Add(CreateOrderItem(data.OrderId, true, "Chair"));
        await context.SaveChangesAsync();
        var service = BuildService(context, new FailingNotificationDispatcher());

        var result = await service.CreateAsync(
            data.OrderId,
            _salesId,
            new CreateProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(201, result.Status);
        Assert.Single(context.ProductionRequestSet);
        Assert.Single(context.ProductionItemSet);
    }

    [Fact]
    public async Task CreateAsync_WhenDateRangeInvalid_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CreateAsync(
            Guid.NewGuid(),
            _salesId,
            new CreateProductionRequestDto
            {
                AssignedTo = _productionId,
                EstimatedStartDate = new DateOnly(2026, 8, 10),
                EstimatedCompletionDate = new DateOnly(2026, 7, 25)
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductionErrorCodes.InvalidProductionRequestDate, result.ErrorCode);
    }

    [Fact]
    public async Task GetAvailableStaffAsync_ReturnsActiveProductionStaffWithWorkload()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        context.ProductionRequestSet.AddRange(
            CreateProductionRequest(data.ProjectId, data.OrderId, _productionId, ProductionRequestStatus.PENDING),
            CreateProductionRequest(data.ProjectId, data.OrderId, _productionId, ProductionRequestStatus.IN_PRODUCTION),
            CreateProductionRequest(data.ProjectId, data.OrderId, _productionId, ProductionRequestStatus.COMPLETED));
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.GetAvailableStaffAsync(
            _salesId,
            new AvailableProductionStaffQueryDto { Search = "production" });

        Assert.Equal(200, result.Status);
        var staff = Assert.Single(result.Data!);
        Assert.Equal(_productionId, staff.AccountId);
        Assert.Equal("ACTIVE", staff.AccountStatus);
        Assert.Equal(2, staff.ActiveRequestCount);
        Assert.Equal(1, staff.PendingReviewRequestCount);
        Assert.Equal(1, staff.InProductionRequestCount);
        Assert.Equal(0, staff.BlockedRequestCount);
        Assert.True(staff.IsAvailable);
    }

    [Fact]
    public async Task GetAvailableStaffAsync_ValidatesFilters()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var tooLong = await service.GetAvailableStaffAsync(
            _salesId,
            new AvailableProductionStaffQueryDto { Search = new string('x', 151) });
        var missingProject = await service.GetAvailableStaffAsync(
            _salesId,
            new AvailableProductionStaffQueryDto { ProjectId = Guid.NewGuid() });
        var missingRequest = await service.GetAvailableStaffAsync(
            _salesId,
            new AvailableProductionStaffQueryDto { ProductionRequestId = Guid.NewGuid() });

        Assert.Equal(ProductionErrorCodes.InvalidProductionStaffFilter, tooLong.ErrorCode);
        Assert.Equal(ProductionErrorCodes.ProjectNotFound, missingProject.ErrorCode);
        Assert.Equal(ProductionErrorCodes.ProductionRequestNotFound, missingRequest.ErrorCode);
    }

    [Fact]
    public async Task Methods_WhenUserUnauthorizedOrForbidden_ReturnExpectedStatus()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.GetAvailableStaffAsync(Guid.Empty, new AvailableProductionStaffQueryDto());
        var forbidden = await service.GetAvailableStaffAsync(_productionId, new AvailableProductionStaffQueryDto());

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
    }

    [Fact]
    public async Task AssignAsync_WhenValid_ReassignsAndNotifies()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        await context.SaveChangesAsync();
        var secondProductionId = Guid.NewGuid();
        AddProductionAccount(context, secondProductionId, AccountStatus.ACTIVE);
        var productionRequest = CreateProductionRequest(
            data.ProjectId,
            data.OrderId,
            _productionId,
            ProductionRequestStatus.IN_PRODUCTION);
        productionRequest.Note = "Initial note";
        context.ProductionRequestSet.Add(productionRequest);
        await context.SaveChangesAsync();
        var dispatcher = new CapturingNotificationDispatcher();
        var service = BuildService(context, dispatcher);

        var result = await service.AssignAsync(
            productionRequest.ProductionRequestId,
            _salesId,
            new AssignProductionRequestDto
            {
                AssignedTo = secondProductionId,
                AssignmentNote = " Reassigned due to workload. "
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(_productionId, result.Data!.PreviousAssignedTo);
        Assert.Equal(secondProductionId, result.Data.AssignedTo);
        Assert.Contains("Reassigned due to workload.", context.ProductionRequestSet.Single().Note, StringComparison.Ordinal);
        var assignedDispatch = Assert.Single(
            dispatcher.Dispatches,
            dispatch => dispatch.Type == NotificationType.ProductionRequestAssigned);
        Assert.Equal(2, assignedDispatch.Receivers.Count);
        Assert.Contains(secondProductionId, assignedDispatch.Receivers);
        Assert.Contains(_salesId, assignedDispatch.Receivers);
    }

    [Fact]
    public async Task AssignAsync_WithSameProductionStaff_SucceedsIdempotently()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        var productionRequest = CreateProductionRequest(
            data.ProjectId,
            data.OrderId,
            _productionId,
            ProductionRequestStatus.PENDING);
        context.ProductionRequestSet.Add(productionRequest);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.AssignAsync(
            productionRequest.ProductionRequestId,
            _salesId,
            new AssignProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(200, result.Status);
        Assert.Equal(_productionId, result.Data!.PreviousAssignedTo);
        Assert.Equal(_productionId, result.Data.AssignedTo);
    }

    [Theory]
    [InlineData(ProductionRequestStatus.COMPLETED)]
    [InlineData(ProductionRequestStatus.CANCELLED)]
    public async Task AssignAsync_WhenRequestClosed_ReturnsBadRequest(ProductionRequestStatus status)
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        var productionRequest = CreateProductionRequest(data.ProjectId, data.OrderId, _productionId, status);
        context.ProductionRequestSet.Add(productionRequest);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.AssignAsync(
            productionRequest.ProductionRequestId,
            _salesId,
            new AssignProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductionErrorCodes.ProductionRequestAlreadyClosed, result.ErrorCode);
    }

    [Fact]
    public async Task AssignAsync_WhenAssigneeInvalidOrInactive_ReturnsExpectedErrors()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.SUSPENDED);
        await context.SaveChangesAsync();
        var customerId = Guid.NewGuid();
        AddCustomerAccount(context, customerId);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var invalidRole = await service.AssignAsync(
            Guid.NewGuid(),
            _salesId,
            new AssignProductionRequestDto { AssignedTo = customerId });
        var inactive = await service.AssignAsync(
            Guid.NewGuid(),
            _salesId,
            new AssignProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(ProductionErrorCodes.InvalidProductionAssignee, invalidRole.ErrorCode);
        Assert.Equal(ProductionErrorCodes.ProductionAssigneeNotActive, inactive.ErrorCode);
    }

    [Fact]
    public async Task AssignAsync_WhenAssigneeEmptyOrRequestMissing_ReturnsExpectedErrors()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var emptyAssignee = await service.AssignAsync(
            Guid.NewGuid(),
            _salesId,
            new AssignProductionRequestDto());
        var missingRequest = await service.AssignAsync(
            Guid.NewGuid(),
            _salesId,
            new AssignProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(ProductionErrorCodes.InvalidProductionAssignee, emptyAssignee.ErrorCode);
        Assert.Equal(ProductionErrorCodes.ProductionRequestNotFound, missingRequest.ErrorCode);
    }

    [Fact]
    public async Task AssignAsync_WhenSalesNotAssigned_ReturnsForbidden()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        var productionRequest = CreateProductionRequest(data.ProjectId, data.OrderId, _productionId, ProductionRequestStatus.IN_PRODUCTION);
        context.ProductionRequestSet.Add(productionRequest);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.AssignAsync(
            productionRequest.ProductionRequestId,
            Guid.NewGuid(),
            new AssignProductionRequestDto { AssignedTo = _productionId });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetQueueAsync_ReturnsVisibleProductionRequestsWithFilters()
    {
        await using var context = CreateContext();
        var own = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        var otherSalesId = Guid.NewGuid();
        var other = SeedOrderProjectRequest(context, otherSalesId, _productionId, ProductionRequestStatus.PENDING, "URGENT");
        var ownRequest = CreateProductionRequest(own.ProjectId, own.OrderId, _productionId, ProductionRequestStatus.PENDING);
        ownRequest.Priority = "NORMAL";
        context.ProductionRequestSet.Add(ownRequest);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var salesQueue = await service.GetQueueAsync(_salesId, new ProductionRequestQueryDto());
        var productionQueue = await service.GetQueueAsync(
            _productionId,
            new ProductionRequestQueryDto
            {
                Status = ProductionRequestStatus.PENDING,
                AssignedTo = _productionId,
                Priority = " urgent "
            });

        Assert.Equal(200, salesQueue.Status);
        Assert.Single(salesQueue.Data!.Items);
        Assert.Equal(ownRequest.ProductionRequestId, salesQueue.Data.Items[0].ProductionRequestId);
        Assert.Equal(200, productionQueue.Status);
        var item = Assert.Single(productionQueue.Data!.Items);
        Assert.Equal(other.ProductionRequestId, item.ProductionRequestId);
        Assert.Equal("PENDING", item.Status);
    }

    [Fact]
    public async Task GetQueueAsync_WhenUnauthorizedOrForbidden_ReturnsExpectedStatus()
    {
        await using var context = CreateContext();
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        var customerId = Guid.NewGuid();
        await context.SaveChangesAsync();
        AddCustomerAccount(context, customerId);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.GetQueueAsync(Guid.Empty, new ProductionRequestQueryDto());
        var forbidden = await service.GetQueueAsync(customerId, new ProductionRequestQueryDto());

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsItemsWhenAuthorized()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        var orderItem = CreateOrderItem(data.OrderId, true, "Display Cabinet");
        var productionRequest = CreateProductionRequest(
            data.ProjectId,
            data.OrderId,
            _productionId,
            ProductionRequestStatus.IN_PRODUCTION);
        context.OrderItemSet.Add(orderItem);
        context.ProductionRequestSet.Add(productionRequest);
        context.ProductionItemSet.Add(CreateProductionItem(productionRequest.ProductionRequestId, orderItem));
        await context.SaveChangesAsync();
        var productionDeadline = new DateOnly(2026, 10, 15);
        var service = BuildService(
            context,
            phaseDeadlines: new CapturingProjectPhaseDeadlineService
            {
                ProductionDeadline = productionDeadline
            });

        var result = await service.GetDetailAsync(productionRequest.ProductionRequestId, _productionId);

        Assert.Equal(200, result.Status);
        Assert.Equal(productionRequest.ProductionRequestId, result.Data!.ProductionRequestId);
        Assert.Equal(productionDeadline, result.Data.ProductionDeadline);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal(orderItem.OrderItemId, item.OrderItemId);
        Assert.Equal("PENDING", item.OrderItemStatus);
    }

    [Fact]
    public async Task GetDetailAsync_WhenMissingOrForbidden_ReturnsExpectedStatus()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        var productionRequest = CreateProductionRequest(
            data.ProjectId,
            data.OrderId,
            _productionId,
            ProductionRequestStatus.PENDING);
        context.ProductionRequestSet.Add(productionRequest);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.GetDetailAsync(productionRequest.ProductionRequestId, Guid.Empty);
        var missing = await service.GetDetailAsync(Guid.NewGuid(), _salesId);
        var forbidden = await service.GetDetailAsync(productionRequest.ProductionRequestId, Guid.NewGuid());

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(ProductionErrorCodes.ProductionRequestNotFound, missing.ErrorCode);
        Assert.Equal(403, forbidden.Status);
    }

    [Fact]
    public async Task StartAsync_WhenPending_UpdatesStatusAndActualStartDate()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        var productionRequest = CreateProductionRequest(data.ProjectId, data.OrderId, _productionId, ProductionRequestStatus.PENDING);
        context.ProductionRequestSet.Add(productionRequest);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.StartAsync(
            productionRequest.ProductionRequestId,
            _productionId,
            new StartProductionRequestDto());

        Assert.Equal(200, result.Status);
        Assert.Equal("IN_PRODUCTION", result.Data!.Status);
        Assert.NotNull(result.Data.ActualStartDate);
        Assert.Equal(ProductionRequestStatus.IN_PRODUCTION, context.ProductionRequestSet.Single().Status);
        Assert.NotNull(context.ProductionRequestSet.Single().ActualStartDate);
    }

    [Fact]
    public async Task StartAsync_WhenInvalidTransition_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        var productionRequest = CreateProductionRequest(
            data.ProjectId,
            data.OrderId,
            _productionId,
            ProductionRequestStatus.COMPLETED);
        context.ProductionRequestSet.Add(productionRequest);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.StartAsync(
            productionRequest.ProductionRequestId,
            _productionId,
            new StartProductionRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductionErrorCodes.InvalidProductionRequestTransition, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateItemStatusAsync_WhenPendingToInProduction_StartsItem()
    {
        await using var context = CreateContext();
        var seeded = SeedProductionItemScenario(context, ProductionItemStatus.PENDING);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.UpdateItemStatusAsync(
            seeded.ProductionItemId,
            _productionId,
            new UpdateProductionItemStatusDto
            {
                Status = ProductionItemStatus.IN_PRODUCTION,
                ProductionNote = "Material preparation started."
            });

        var item = context.ProductionItemSet.Single();
        Assert.Equal(200, result.Status);
        Assert.Equal("IN_PRODUCTION", result.Data!.Status);
        Assert.Equal(ProductionItemStatus.IN_PRODUCTION, item.Status);
        Assert.NotNull(item.StartedAt);
        Assert.Contains("Material preparation started.", item.ProductionNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateItemStatusAsync_WhenInProductionToCompleted_CompletesItem()
    {
        await using var context = CreateContext();
        var seeded = SeedProductionItemScenario(context, ProductionItemStatus.IN_PRODUCTION);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.UpdateItemStatusAsync(
            seeded.ProductionItemId,
            _productionId,
            new UpdateProductionItemStatusDto
            {
                Status = ProductionItemStatus.COMPLETED,
                ProductionNote = "Item completed in full."
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("COMPLETED", result.Data!.Status);
        Assert.NotNull(result.Data.CompletedAt);
        Assert.Equal(OrderItemStatus.IN_PRODUCTION, context.OrderItemSet.Single().Status);
    }

    [Fact]
    public async Task UpdateItemStatusAsync_WhenCancelled_NotifiesSalesAndKeepsOrderItemStatus()
    {
        await using var context = CreateContext();
        var seeded = SeedProductionItemScenario(context, ProductionItemStatus.IN_PRODUCTION);
        await context.SaveChangesAsync();
        var dispatcher = new CapturingNotificationDispatcher();
        var service = BuildService(context, dispatcher);

        var result = await service.UpdateItemStatusAsync(
            seeded.ProductionItemId,
            _productionId,
            new UpdateProductionItemStatusDto
            {
                Status = ProductionItemStatus.CANCELLED,
                CancellationReason = "Material unavailable."
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("CANCELLED", result.Data!.Status);
        Assert.Equal("Material unavailable.", result.Data.CancellationReason);
        Assert.Equal(OrderItemStatus.IN_PRODUCTION, context.OrderItemSet.Single().Status);
        Assert.Equal(NotificationType.ProductionItemCancelled, dispatcher.NotificationType);
        Assert.Equal(_salesId, Assert.Single(dispatcher.ReceiverIds));
    }

    [Fact]
    public async Task UpdateItemStatusAsync_WhenCancelledAndNotificationFails_StillCancelsItem()
    {
        await using var context = CreateContext();
        var seeded = SeedProductionItemScenario(context, ProductionItemStatus.IN_PRODUCTION);
        await context.SaveChangesAsync();
        var service = BuildService(context, new FailingNotificationDispatcher());

        var result = await service.UpdateItemStatusAsync(
            seeded.ProductionItemId,
            _productionId,
            new UpdateProductionItemStatusDto
            {
                Status = ProductionItemStatus.CANCELLED,
                CancellationReason = "Material unavailable."
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(ProductionItemStatus.CANCELLED, context.ProductionItemSet.Single().Status);
    }

    [Fact]
    public async Task UpdateItemStatusAsync_WhenInvalid_ReturnsExpectedErrors()
    {
        await using var context = CreateContext();
        var seeded = SeedProductionItemScenario(context, ProductionItemStatus.COMPLETED);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.UpdateItemStatusAsync(
            seeded.ProductionItemId,
            Guid.Empty,
            new UpdateProductionItemStatusDto { Status = ProductionItemStatus.CANCELLED });
        var forbidden = await service.UpdateItemStatusAsync(
            seeded.ProductionItemId,
            _salesId,
            new UpdateProductionItemStatusDto { Status = ProductionItemStatus.CANCELLED });
        var missing = await service.UpdateItemStatusAsync(
            Guid.NewGuid(),
            _productionId,
            new UpdateProductionItemStatusDto { Status = ProductionItemStatus.CANCELLED });
        var nullStatus = await service.UpdateItemStatusAsync(
            seeded.ProductionItemId,
            _productionId,
            new UpdateProductionItemStatusDto());
        var terminal = await service.UpdateItemStatusAsync(
            seeded.ProductionItemId,
            _productionId,
            new UpdateProductionItemStatusDto { Status = ProductionItemStatus.CANCELLED });

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(ProductionErrorCodes.ProductionItemNotFound, missing.ErrorCode);
        Assert.Equal(ProductionErrorCodes.InvalidProductionItemTransition, nullStatus.ErrorCode);
        Assert.Equal(ProductionErrorCodes.InvalidProductionItemTransition, terminal.ErrorCode);
    }

    [Fact]
    public async Task UpdateItemStatusAsync_WhenCancellationReasonMissing_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var seeded = SeedProductionItemScenario(context, ProductionItemStatus.PENDING);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.UpdateItemStatusAsync(
            seeded.ProductionItemId,
            _productionId,
            new UpdateProductionItemStatusDto { Status = ProductionItemStatus.CANCELLED });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductionErrorCodes.ProductionCancellationReasonRequired, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenItemsResolved_MovesToDelivery()
    {
        await using var context = CreateContext();
        var seeded = SeedCompletionScenario(context);
        await context.SaveChangesAsync();
        var dispatcher = new CapturingNotificationDispatcher();
        var phaseDeadlines = new CapturingProjectPhaseDeadlineService();
        var service = BuildService(context, dispatcher, phaseDeadlines);

        var result = await service.CompleteAsync(seeded.ProductionRequestId, _productionId);

        Assert.Equal(200, result.Status);
        Assert.Equal("COMPLETED", result.Data!.ProductionStatus);
        Assert.Equal("READY_FOR_DELIVERY", result.Data.OrderStatus);
        Assert.Equal("READY_FOR_DELIVERY", result.Data.ProjectStatus);
        Assert.Equal(1, result.Data.ReadyOrderItemCount);
        Assert.Equal(1, result.Data.UnavailableOrderItemCount);
        Assert.Equal(10_000_000m, result.Data.FinalTotalAmount);
        Assert.Equal(3_000_000m, result.Data.PaidAmount);
        Assert.Equal(7_000_000m, result.Data.RemainingAmount);
        Assert.Equal(OrderItemStatus.READY, context.OrderItemSet.Single(item => item.OrderItemId == seeded.CompletedOrderItemId).Status);
        Assert.Equal(OrderItemStatus.UNAVAILABLE, context.OrderItemSet.Single(item => item.OrderItemId == seeded.CancelledOrderItemId).Status);
        Assert.Equal(ProjectStatus.READY_FOR_DELIVERY, context.ProjectSet.Single().Status);
        Assert.Equal(seeded.ProjectId, phaseDeadlines.ProjectId);
        Assert.Equal(ProjectPhaseType.PRODUCTION, phaseDeadlines.Phase);
        Assert.Contains(
            dispatcher.Dispatches,
            dispatch => dispatch.Type == NotificationType.ProductionRequestCompleted &&
                        dispatch.Receivers.Contains(_salesId));
        Assert.Contains(
            dispatcher.Dispatches,
            dispatch => dispatch.Type == NotificationType.OrderUpdated &&
                        dispatch.Receivers.Contains(_salesId));
    }

    [Fact]
    public async Task CompleteAsync_WhenNotificationFails_DoesNotRollbackCompletion()
    {
        await using var context = CreateContext();
        var seeded = SeedCompletionScenario(context);
        await context.SaveChangesAsync();
        var service = BuildService(context, new FailingNotificationDispatcher());

        var result = await service.CompleteAsync(seeded.ProductionRequestId, _productionId);

        Assert.Equal(200, result.Status);
        Assert.Equal(ProductionRequestStatus.COMPLETED, context.ProductionRequestSet.Single().Status);
        Assert.Equal(OrderStatus.READY_FOR_DELIVERY, context.OrderSet.Single().Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenAlreadyCompleted_ReturnsCurrentStateIdempotently()
    {
        await using var context = CreateContext();
        var seeded = SeedCompletionScenario(context);
        var request = context.ProductionRequestSet.Local.Single();
        var order = context.OrderSet.Local.Single();
        var project = context.ProjectSet.Local.Single();
        request.Status = ProductionRequestStatus.COMPLETED;
        order.Status = OrderStatus.READY_FOR_DELIVERY;
        project.Status = ProjectStatus.READY_FOR_DELIVERY;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CompleteAsync(seeded.ProductionRequestId, _productionId);

        Assert.Equal(200, result.Status);
        Assert.Equal("COMPLETED", result.Data!.ProductionStatus);
        Assert.Equal(0, result.Data.ReadyOrderItemCount);
        Assert.Equal(0, result.Data.UnavailableOrderItemCount);
    }

    [Fact]
    public async Task CompleteAsync_WhenOrderItemsAlreadySynced_AllowsIdempotentCompletion()
    {
        await using var context = CreateContext();
        var seeded = SeedCompletionScenario(context);
        context.OrderItemSet.Local.Single(item => item.OrderItemId == seeded.CompletedOrderItemId).Status =
            OrderItemStatus.READY;
        context.OrderItemSet.Local.Single(item => item.OrderItemId == seeded.CancelledOrderItemId).Status =
            OrderItemStatus.UNAVAILABLE;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CompleteAsync(seeded.ProductionRequestId, _productionId);

        Assert.Equal(200, result.Status);
        Assert.Equal("COMPLETED", result.Data!.ProductionStatus);
        Assert.Equal("READY_FOR_DELIVERY", result.Data.OrderStatus);
    }

    [Fact]
    public async Task CompleteAsync_WhenProductionItemOrderItemMissing_ReturnsMappingInvalid()
    {
        await using var context = CreateContext();
        var seeded = SeedCompletionScenario(context);
        var orderItem = context.OrderItemSet.Local.Single(item => item.OrderItemId == seeded.CompletedOrderItemId);
        context.OrderItemSet.Remove(orderItem);
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CompleteAsync(seeded.ProductionRequestId, _productionId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductionErrorCodes.OrderItemMappingInvalid, result.ErrorCode);
        Assert.Equal(ProductionRequestStatus.IN_PRODUCTION, context.ProductionRequestSet.Single().Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenItemsNotResolved_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var seeded = SeedCompletionScenario(context);
        context.ProductionItemSet.Local.First().Status = ProductionItemStatus.IN_PRODUCTION;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var result = await service.CompleteAsync(seeded.ProductionRequestId, _productionId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProductionErrorCodes.ProductionItemsNotResolved, result.ErrorCode);
        Assert.Equal(ProductionRequestStatus.IN_PRODUCTION, context.ProductionRequestSet.Single().Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenInvalid_ReturnsExpectedErrors()
    {
        await using var context = CreateContext();
        var seeded = SeedCompletionScenario(context);
        context.ProductionRequestSet.Local.Single().Status = ProductionRequestStatus.PENDING;
        await context.SaveChangesAsync();
        var service = BuildService(context);

        var unauthorized = await service.CompleteAsync(seeded.ProductionRequestId, Guid.Empty);
        var forbidden = await service.CompleteAsync(seeded.ProductionRequestId, _salesId);
        var missing = await service.CompleteAsync(Guid.NewGuid(), _productionId);
        var invalidTransition = await service.CompleteAsync(seeded.ProductionRequestId, _productionId);

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(403, forbidden.Status);
        Assert.Equal(404, missing.Status);
        Assert.Equal(ProductionErrorCodes.ProductionRequestNotFound, missing.ErrorCode);
        Assert.Equal(400, invalidTransition.Status);
        Assert.Equal(ProductionErrorCodes.InvalidProductionRequestTransition, invalidTransition.ErrorCode);
    }

    private static ProductionRequestService BuildService(
        AppDbContext context,
        INotificationDispatcher? dispatcher = null,
        IProjectPhaseDeadlineService? phaseDeadlines = null)
    {
        return new ProductionRequestService(
            new ProductionRequestRepository(context),
            new OrderRepository(context),
            new ProjectRepository(context),
            new PaymentRepository(context),
            new ProductionRequestServiceDependencies(
                new InMemoryUnitOfWork(context),
                dispatcher,
                logger: null,
                phaseDeadlines ?? new CapturingProjectPhaseDeadlineService()));
    }

    private SeededData SeedBase(AppDbContext context, OrderStatus orderStatus, PaymentStatus paymentStatus)
    {
        SeedRolesAndAccounts(context, AccountStatus.ACTIVE);
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = _salesId,
            ProjectName = "Cafe Project",
            Status = ProjectStatus.ORDER_CONFIRMED
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            SalesId = _salesId,
            OrderCode = "ORD-001",
            OriginalTotalAmount = 10_000_000m,
            FinalTotalAmount = 10_000_000m,
            PaidAmount = 0m,
            RemainingAmount = 10_000_000m,
            Status = orderStatus
        });
        context.PaymentSet.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = projectId,
            OrderId = orderId,
            PaymentCode = "PAY-001",
            PaymentType = PaymentType.DEPOSIT,
            Status = paymentStatus,
            Amount = 100m
        });
        return new SeededData(projectId, orderId);
    }

    private void SeedRolesAndAccounts(AppDbContext context, AccountStatus productionStatus)
    {
        var salesRole = CreateRole("SALES");
        var productionRole = CreateRole("PRODUCTION");
        context.RoleSet.AddRange(salesRole, productionRole, CreateRole("CUSTOMER"));
        context.AccountSet.AddRange(
            CreateAccount(_salesId, salesRole.RoleId, "sales@example.com", "Sales User", AccountStatus.ACTIVE),
            CreateAccount(_productionId, productionRole.RoleId, "production@example.com", "Production User", productionStatus));
    }

    private static void AddProductionAccount(
        AppDbContext context,
        Guid accountId,
        AccountStatus status)
    {
        var roleId = context.RoleSet.Single(role => role.RoleName == "PRODUCTION").RoleId;
        context.AccountSet.Add(CreateAccount(
            accountId,
            roleId,
            $"{accountId:N}@production.example.com",
            "Second Production",
            status));
    }

    private static void AddCustomerAccount(AppDbContext context, Guid accountId)
    {
        var roleId = context.RoleSet.Single(role => role.RoleName == "CUSTOMER").RoleId;
        context.AccountSet.Add(CreateAccount(
            accountId,
            roleId,
            $"{accountId:N}@customer.example.com",
            "Customer User",
            AccountStatus.ACTIVE));
    }

    private static ProductionRequest SeedOrderProjectRequest(
        AppDbContext context,
        Guid salesId,
        Guid productionId,
        ProductionRequestStatus status,
        string priority)
    {
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var request = CreateProductionRequest(projectId, orderId, productionId, status);
        request.Priority = priority;
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectName = "Other Project",
            Status = ProjectStatus.IN_PRODUCTION
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            SalesId = salesId,
            OrderCode = $"ORD-{orderId:N}",
            Status = OrderStatus.IN_PRODUCTION
        });
        context.ProductionRequestSet.Add(request);
        return request;
    }

    private SeededProductionItem SeedProductionItemScenario(
        AppDbContext context,
        ProductionItemStatus itemStatus)
    {
        var data = SeedBase(context, OrderStatus.DEPOSIT_PAID, PaymentStatus.PAID);
        var orderItem = CreateOrderItem(data.OrderId, true, "Cafe Chair");
        orderItem.Status = OrderItemStatus.IN_PRODUCTION;
        var productionRequest = CreateProductionRequest(
            data.ProjectId,
            data.OrderId,
            _productionId,
            ProductionRequestStatus.IN_PRODUCTION);
        productionRequest.ProductionCode = "PRD-001";
        var productionItem = CreateProductionItem(productionRequest.ProductionRequestId, orderItem);
        productionItem.Status = itemStatus;
        context.OrderItemSet.Add(orderItem);
        context.ProductionRequestSet.Add(productionRequest);
        context.ProductionItemSet.Add(productionItem);
        return new SeededProductionItem(productionItem.ProductionItemId);
    }

    private SeededCompletion SeedCompletionScenario(
        AppDbContext context,
        decimal paidAmount = 3_000_000m)
    {
        var data = SeedBase(context, OrderStatus.IN_PRODUCTION, PaymentStatus.PAID);
        var order = context.OrderSet.Local.Single();
        var project = context.ProjectSet.Local.Single();
        order.PaidAmount = paidAmount;
        order.RemainingAmount = order.FinalTotalAmount - paidAmount;
        project.Status = ProjectStatus.IN_PRODUCTION;
        var completedItem = CreateOrderItem(data.OrderId, true, "Counter");
        var cancelledItem = CreateOrderItem(data.OrderId, true, "Chair");
        completedItem.Status = OrderItemStatus.IN_PRODUCTION;
        cancelledItem.Status = OrderItemStatus.IN_PRODUCTION;
        completedItem.SubtotalAmount = 3_000_000m;
        cancelledItem.SubtotalAmount = 2_000_000m;
        var productionRequest = CreateProductionRequest(
            data.ProjectId,
            data.OrderId,
            _productionId,
            ProductionRequestStatus.IN_PRODUCTION);
        var completedProductionItem = CreateProductionItem(productionRequest.ProductionRequestId, completedItem);
        var cancelledProductionItem = CreateProductionItem(productionRequest.ProductionRequestId, cancelledItem);
        completedProductionItem.Status = ProductionItemStatus.COMPLETED;
        cancelledProductionItem.Status = ProductionItemStatus.CANCELLED;
        cancelledProductionItem.CancellationReason = "Item unavailable.";
        context.OrderItemSet.AddRange(completedItem, cancelledItem);
        context.ProductionRequestSet.Add(productionRequest);
        context.ProductionItemSet.AddRange(completedProductionItem, cancelledProductionItem);

        return new SeededCompletion(
            data.ProjectId,
            productionRequest.ProductionRequestId,
            completedItem.OrderItemId,
            cancelledItem.OrderItemId);
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

    private static Account CreateAccount(
        Guid accountId,
        Guid roleId,
        string email,
        string fullName,
        AccountStatus status)
    {
        return new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = fullName,
            Status = status
        };
    }

    private static OrderItem CreateOrderItem(
        Guid orderId,
        bool isProductItem,
        string productName)
    {
        return WithFinancialSnapshot(new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = isProductItem ? Guid.NewGuid() : null,
            ProductNameSnapshot = productName,
            ProductVersionNameSnapshot = $"{productName} Version",
            Quantity = 2,
            Status = OrderItemStatus.PENDING,
            ProductionNote = "Use premium finish"
        });
    }

    private static OrderItem WithFinancialSnapshot(OrderItem item, decimal subtotalAmount = 2_000_000m)
    {
        item.UnitPrice = subtotalAmount / Math.Max(item.Quantity ?? 1, 1);
        item.DiscountAmount = 0m;
        item.SubtotalAmount = subtotalAmount;
        return item;
    }

    private static ProductionRequest CreateProductionRequest(
        Guid projectId,
        Guid orderId,
        Guid assignedTo,
        ProductionRequestStatus status)
    {
        return new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProjectId = projectId,
            OrderId = orderId,
            AssignedTo = assignedTo,
            Status = status
        };
    }

    private static ProductionItem CreateProductionItem(Guid productionRequestId, OrderItem orderItem)
    {
        return new ProductionItem
        {
            ProductionItemId = Guid.NewGuid(),
            ProductionRequestId = productionRequestId,
            OrderItemId = orderItem.OrderItemId,
            ProductVersionId = orderItem.ProductVersionId,
            ProductNameSnapshot = orderItem.ProductNameSnapshot,
            ProductVersionNameSnapshot = orderItem.ProductVersionNameSnapshot,
            Quantity = orderItem.Quantity,
            Status = ProductionItemStatus.PENDING,
            ProductionNote = orderItem.ProductionNote
        };
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public InMemoryUnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CapturingNotificationDispatcher : INotificationDispatcher
    {
        public NotificationType? NotificationType { get; private set; }
        public List<Guid> ReceiverIds { get; } = [];
        public List<(NotificationType Type, IReadOnlyList<Guid> Receivers)> Dispatches { get; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            NotificationDispatchRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            var receivers = receiverIds.ToList();
            NotificationType = type;
            ReceiverIds.AddRange(receivers);
            Dispatches.Add((type, receivers));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingNotificationDispatcher : INotificationDispatcher
    {
        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            NotificationDispatchRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Notification failed.");
        }
    }

    private sealed class CapturingProjectPhaseDeadlineService : IProjectPhaseDeadlineService
    {
        public bool HasProductionDeadline { get; init; } = true;

        public Guid ProjectId { get; private set; }
        public ProjectPhaseType Phase { get; private set; }

        public Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> UpsertAsync(
            Guid projectId,
            Guid currentUserId,
            UpsertProjectPhaseDeadlinesRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(new ProjectPhaseDeadlinePlanDto()));
        }

        public Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> GetAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(new ProjectPhaseDeadlinePlanDto()));
        }

        public Task MarkStartedOnceAsync(
            Guid projectId,
            ProjectPhaseType phase,
            DateTime startedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkCompletedOnceAsync(
            Guid projectId,
            ProjectPhaseType phase,
            DateTime completedAt,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            Phase = phase;
            return Task.CompletedTask;
        }

        public Task<ServiceResult<ProjectProductionPhaseDeadlineResponseDto>> UpsertProductionDeadlineAsync(
            Guid projectId,
            Guid currentUserId,
            UpsertProductionPhaseDeadlineRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Success(
                new ProjectProductionPhaseDeadlineResponseDto()));
        }

        public Task<ServiceResult<DateOnly>> StageProposalDeadlineForDesignerAssignmentAsync(
            Guid projectId,
            Guid currentUserId,
            DateOnly proposalDeadline,
            DateOnly? targetCompletionDate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<DateOnly>.Success(proposalDeadline));
        }

        public Task<bool> HasProductionDeadlineAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HasProductionDeadline);
        }

        public DateOnly? ProductionDeadline { get; init; }

        public Task<DateOnly?> GetProductionDeadlineAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProductionDeadline);
        }
    }

    private sealed record SeededData(Guid ProjectId, Guid OrderId);

    private sealed record SeededProductionItem(Guid ProductionItemId);

    private sealed record SeededCompletion(
        Guid ProjectId,
        Guid ProductionRequestId,
        Guid CompletedOrderItemId,
        Guid CancelledOrderItemId);
}
