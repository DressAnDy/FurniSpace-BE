#nullable enable

#pragma warning disable CS0618 // Obsolete legacy delivery endpoints under test

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Orders;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.Production;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class OrdersControllerTests
{
    [Fact]
    public void GetByProject_RequiresProjectRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.GetByProject));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public void CreateDepositPayment_RequiresCustomerSalesOrAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.CreateDepositPayment));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER,SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void CreateRemainingPayment_RequiresSalesOrAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.CreateRemainingPayment));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void PrepareFinalPayment_RequiresAdminOnly()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.PrepareFinalPayment));

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void Complete_RequiresSalesOrAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.Complete));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void CreateProductionRequest_RequiresSalesOrAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.CreateProductionRequest));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void CreateDeliveryBatch_RequiresSalesProductionOrAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.CreateDeliveryBatch));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task CreateDeliveryBatch_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var service = new FakeOrderService(
            createDeliveryBatchResult: ServiceResult<DeliveryDetailDto>.Created(
                new DeliveryDetailDto { DeliveryId = deliveryId, OrderId = orderId },
                "Created"));
        var controller = CreateController(
            service,
            new FakePaymentService(),
            new FakeProductionRequestService(),
            Guid.NewGuid());

        var result = await controller.CreateDeliveryBatch(
            orderId,
            new CreateDeliveryBatchRequestDto
            {
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = Guid.NewGuid(), Quantity = 1 }]
            });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(orderId, service.OrderId);
        Assert.NotNull(service.CreateDeliveryBatchRequest);
    }

    [Fact]
    public async Task GetDeliveries_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var service = new FakeOrderService(
            getDeliveriesResult: ServiceResult<DeliveryListResponseDto>.Success(new DeliveryListResponseDto()));
        var controller = CreateController(
            service,
            new FakePaymentService(),
            new FakeProductionRequestService(),
            Guid.NewGuid());

        var result = await controller.GetDeliveries(orderId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(orderId, service.OrderId);
    }

    [Fact]
    public void StartDelivery_RequiresAdminOnly()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.StartDelivery));

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void CompleteDelivery_RequiresAdminOnly()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.CompleteDelivery));

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void ConfirmDelivery_RequiresCustomer()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.ConfirmDelivery));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER", authorize.Roles);
    }

    [Fact]
    public async Task GetByProject_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeOrderService(), new FakePaymentService(), new FakeProductionRequestService(), userId: null);

        var result = await controller.GetByProject(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetByProject_ReturnsServiceResult()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            getByProjectResult: ServiceResult<OrderListResponseDto>.Success(
                new OrderListResponseDto { Items = [] },
                "ok"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.GetByProject(projectId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateDepositPayment_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var paymentService = new FakePaymentService(
            createDepositResult: ServiceResult<PaymentDetailDto>.Created(
                new PaymentDetailDto { PaymentId = Guid.NewGuid(), PaymentType = PaymentType.DEPOSIT },
                "created"));
        var controller = CreateController(new FakeOrderService(), paymentService, new FakeProductionRequestService(), userId);

        var result = await controller.CreateDepositPayment(
            orderId,
            new CreateOrderDepositPaymentRequestDto());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetDetail_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeOrderService(), new FakePaymentService(), new FakeProductionRequestService(), userId: null);

        var result = await controller.GetDetail(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetDetail_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            getDetailResult: ServiceResult<OrderDetailDto>.Success(
                new OrderDetailDto { OrderId = orderId },
                "ok"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.GetDetail(orderId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateRemainingPayment_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var paymentService = new FakePaymentService(
            createRemainingResult: ServiceResult<PaymentDetailDto>.Created(
                new PaymentDetailDto { PaymentType = PaymentType.REMAINING_PAYMENT },
                "created"));
        var controller = CreateController(new FakeOrderService(), paymentService, new FakeProductionRequestService(), userId);

        var result = await controller.CreateRemainingPayment(
            orderId,
            new CreateOrderRemainingPaymentRequestDto());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
    }

    [Fact]
    public async Task PrepareFinalPayment_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            prepareFinalPaymentResult: ServiceResult<OrderFinalPaymentPreparationDto>.Success(
                new OrderFinalPaymentPreparationDto { OrderId = orderId, RequiresRemainingPayment = true },
                "prepared"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.PrepareFinalPayment(orderId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(orderId, orderService.OrderId);
        Assert.Equal(userId, orderService.CurrentUserId);
    }

    [Fact]
    public async Task PrepareFinalPayment_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.PrepareFinalPayment(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Complete_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            completeResult: ServiceResult<OrderCompletionDto>.Success(
                new OrderCompletionDto { OrderId = orderId, OrderStatus = "COMPLETED" },
                "completed"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.Complete(orderId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(orderId, orderService.OrderId);
        Assert.Equal(userId, orderService.CurrentUserId);
    }

    [Fact]
    public async Task Complete_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.Complete(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CreateProductionRequest_ReturnsServiceResultAndPassesRequest()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new CreateProductionRequestDto { AssignedTo = Guid.NewGuid(), Priority = "NORMAL" };
        var productionService = new FakeProductionRequestService(
            createResult: ServiceResult<ProductionRequestCreatedDto>.Created(
                new ProductionRequestCreatedDto { OrderId = orderId },
                "created"));
        var controller = CreateController(new FakeOrderService(), new FakePaymentService(), productionService, userId);

        var result = await controller.CreateProductionRequest(orderId, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(orderId, productionService.OrderId);
        Assert.Equal(userId, productionService.CurrentUserId);
        Assert.Same(request, productionService.CreateRequest);
    }

    [Fact]
    public async Task CreateProductionRequest_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.CreateProductionRequest(
            Guid.NewGuid(),
            new CreateProductionRequestDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task StartDelivery_ReturnsServiceResult()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            startDeliveryResult: ServiceResult<OrderDeliveryStartDto>.Success(
                new OrderDeliveryStartDto { OrderId = orderId },
                "started"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.StartDelivery(orderId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(orderId, orderService.OrderId);
        Assert.Equal(userId, orderService.CurrentUserId);
    }

    [Fact]
    public async Task StartDelivery_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.StartDelivery(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CompleteDelivery_ReturnsServiceResult()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            completeDeliveryResult: ServiceResult<OrderDeliveryCompletionDto>.Success(
                new OrderDeliveryCompletionDto { OrderId = orderId, DeliveredItemCount = 2 },
                "completed"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.CompleteDelivery(orderId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(orderId, orderService.OrderId);
        Assert.Equal(userId, orderService.CurrentUserId);
    }

    [Fact]
    public async Task CompleteDelivery_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.CompleteDelivery(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ConfirmDelivery_ReturnsServiceResult()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            confirmDeliveryResult: ServiceResult<OrderDeliveryConfirmationDto>.Success(
                new OrderDeliveryConfirmationDto { OrderId = orderId },
                "confirmed"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.ConfirmDelivery(orderId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(orderId, orderService.OrderId);
        Assert.Equal(userId, orderService.CurrentUserId);
    }

    [Fact]
    public async Task ConfirmDelivery_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.ConfirmDelivery(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void GetDeliveryDetail_RequiresProjectRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.GetDeliveryDetail));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER,SALES,PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public void CompleteDeliveryBatch_RequiresSalesProductionOrAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.CompleteDeliveryBatch));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetDeliveryDetail_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var service = new FakeOrderService(
            getDeliveryDetailResult: ServiceResult<DeliveryDetailDto>.Success(
                new DeliveryDetailDto { DeliveryId = deliveryId, OrderId = orderId }));
        var controller = CreateController(
            service,
            new FakePaymentService(),
            new FakeProductionRequestService(),
            Guid.NewGuid());

        var result = await controller.GetDeliveryDetail(orderId, deliveryId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(orderId, service.OrderId);
    }

    [Fact]
    public async Task GetDeliveryDetail_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.GetDeliveryDetail(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CompleteDeliveryBatch_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var service = new FakeOrderService(
            completeDeliveryBatchResult: ServiceResult<DeliveryBatchCompletionDto>.Success(
                new DeliveryBatchCompletionDto { DeliveryId = deliveryId, OrderId = orderId },
                "completed"));
        var controller = CreateController(
            service,
            new FakePaymentService(),
            new FakeProductionRequestService(),
            Guid.NewGuid());

        var result = await controller.CompleteDeliveryBatch(orderId, deliveryId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(orderId, service.OrderId);
    }

    [Fact]
    public void GetDeliveryTracking_RequiresProjectRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.GetDeliveryTracking));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER,SALES,PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetDeliveryTracking_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            getDeliveryTrackingResult: ServiceResult<OrderDeliveryTrackingDto>.Success(
                new OrderDeliveryTrackingDto
                {
                    OrderId = orderId,
                    Summary = new OrderDeliveryTrackingSummaryDto { RemainingQuantity = 4 }
                },
                "tracked"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.GetDeliveryTracking(orderId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(orderId, orderService.OrderId);
        Assert.Equal(userId, orderService.CurrentUserId);
    }

    [Fact]
    public async Task GetDeliveryTracking_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.GetDeliveryTracking(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CompleteDeliveryBatch_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.CompleteDeliveryBatch(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CreateDeliveryBatch_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.CreateDeliveryBatch(
            Guid.NewGuid(),
            new CreateDeliveryBatchRequestDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetDeliveries_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.GetDeliveries(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    private static AuthorizeAttribute? GetMethodAuthorizeAttribute(string methodName)
    {
        var method = typeof(OrdersController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == methodName);

        return method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
    }

    private static OrdersController CreateController(
        FakeOrderService orderService,
        FakePaymentService paymentService,
        FakeProductionRequestService productionService,
        Guid? userId)
    {
        var controller = new OrdersController(orderService, paymentService, productionService);
        if (userId.HasValue)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                    ], "TestAuth"))
                }
            };
        }

        return controller;
    }

    private sealed class FakeProductionRequestService : IProductionRequestService
    {
        private readonly ServiceResult<ProductionRequestCreatedDto>? _createResult;

        public FakeProductionRequestService(ServiceResult<ProductionRequestCreatedDto>? createResult = null)
        {
            _createResult = createResult;
        }

        public Guid OrderId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public CreateProductionRequestDto? CreateRequest { get; private set; }

        public Task<ServiceResult<ProductionRequestCreatedDto>> CreateAsync(
            Guid orderId,
            Guid currentUserId,
            CreateProductionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            CreateRequest = request;
            return Task.FromResult(
                _createResult ?? ServiceResult<ProductionRequestCreatedDto>.Unauthorized());
        }

        public Task<ServiceResult<List<AvailableProductionStaffDto>>> GetAvailableStaffAsync(
            Guid currentUserId,
            AvailableProductionStaffQueryDto query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<List<AvailableProductionStaffDto>>.Unauthorized());
        }

        public Task<ServiceResult<ProductionRequestAssignmentDto>> AssignAsync(
            Guid productionRequestId,
            Guid currentUserId,
            AssignProductionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestAssignmentDto>.Unauthorized());
        }

        public Task<ServiceResult<ProductionRequestListResponseDto>> GetQueueAsync(
            Guid currentUserId,
            ProductionRequestQueryDto query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestListResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<ProductionRequestDetailDto>> GetDetailAsync(
            Guid productionRequestId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<ProductionRequestStatusDto>> StartAsync(
            Guid productionRequestId,
            Guid currentUserId,
            StartProductionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestStatusDto>.Unauthorized());
        }

        public Task<ServiceResult<ProductionItemStatusDto>> UpdateItemStatusAsync(
            Guid productionItemId,
            Guid currentUserId,
            UpdateProductionItemStatusDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionItemStatusDto>.Unauthorized());
        }
    }

    private sealed class FakeOrderService : IOrderService
    {
        private readonly ServiceResult<OrderListResponseDto>? _getByProjectResult;
        private readonly ServiceResult<OrderDetailDto>? _getDetailResult;
        private readonly ServiceResult<OrderDeliveryStartDto>? _startDeliveryResult;
        private readonly ServiceResult<OrderDeliveryCompletionDto>? _completeDeliveryResult;
        private readonly ServiceResult<OrderDeliveryConfirmationDto>? _confirmDeliveryResult;
        private readonly ServiceResult<OrderFinalPaymentPreparationDto>? _prepareFinalPaymentResult;
        private readonly ServiceResult<OrderCompletionDto>? _completeResult;
        private readonly ServiceResult<DeliveryDetailDto>? _createDeliveryBatchResult;
        private readonly ServiceResult<DeliveryListResponseDto>? _getDeliveriesResult;
        private readonly ServiceResult<DeliveryDetailDto>? _getDeliveryDetailResult;
        private readonly ServiceResult<DeliveryBatchCompletionDto>? _completeDeliveryBatchResult;
        private readonly ServiceResult<OrderDeliveryTrackingDto>? _getDeliveryTrackingResult;

        public FakeOrderService(
            ServiceResult<OrderListResponseDto>? getByProjectResult = null,
            ServiceResult<OrderDetailDto>? getDetailResult = null,
            ServiceResult<OrderDeliveryStartDto>? startDeliveryResult = null,
            ServiceResult<OrderDeliveryCompletionDto>? completeDeliveryResult = null,
            ServiceResult<OrderDeliveryConfirmationDto>? confirmDeliveryResult = null,
            ServiceResult<OrderFinalPaymentPreparationDto>? prepareFinalPaymentResult = null,
            ServiceResult<OrderCompletionDto>? completeResult = null,
            ServiceResult<DeliveryDetailDto>? createDeliveryBatchResult = null,
            ServiceResult<DeliveryListResponseDto>? getDeliveriesResult = null,
            ServiceResult<DeliveryDetailDto>? getDeliveryDetailResult = null,
            ServiceResult<DeliveryBatchCompletionDto>? completeDeliveryBatchResult = null,
            ServiceResult<OrderDeliveryTrackingDto>? getDeliveryTrackingResult = null)
        {
            _getByProjectResult = getByProjectResult;
            _getDetailResult = getDetailResult;
            _startDeliveryResult = startDeliveryResult;
            _completeDeliveryResult = completeDeliveryResult;
            _confirmDeliveryResult = confirmDeliveryResult;
            _prepareFinalPaymentResult = prepareFinalPaymentResult;
            _completeResult = completeResult;
            _createDeliveryBatchResult = createDeliveryBatchResult;
            _getDeliveriesResult = getDeliveriesResult;
            _getDeliveryDetailResult = getDeliveryDetailResult;
            _completeDeliveryBatchResult = completeDeliveryBatchResult;
            _getDeliveryTrackingResult = getDeliveryTrackingResult;
        }

        public Guid OrderId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public CreateDeliveryBatchRequestDto? CreateDeliveryBatchRequest { get; private set; }

        public Task<ServiceResult<OrderListResponseDto>> GetByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _getByProjectResult ?? ServiceResult<OrderListResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderDetailDto>> GetDetailAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _getDetailResult ?? ServiceResult<OrderDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderDeliveryStartDto>> StartDeliveryAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _startDeliveryResult ?? ServiceResult<OrderDeliveryStartDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderDeliveryCompletionDto>> CompleteDeliveryAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _completeDeliveryResult ?? ServiceResult<OrderDeliveryCompletionDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderDeliveryConfirmationDto>> ConfirmDeliveryAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _confirmDeliveryResult ?? ServiceResult<OrderDeliveryConfirmationDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderFinalPaymentPreparationDto>> PrepareFinalPaymentAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _prepareFinalPaymentResult ?? ServiceResult<OrderFinalPaymentPreparationDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderCompletionDto>> CompleteAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _completeResult ?? ServiceResult<OrderCompletionDto>.Unauthorized());
        }

        public Task<ServiceResult<DeliveryDetailDto>> CreateDeliveryBatchAsync(
            Guid orderId,
            Guid currentUserId,
            CreateDeliveryBatchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            CreateDeliveryBatchRequest = request;
            return Task.FromResult(
                _createDeliveryBatchResult ?? ServiceResult<DeliveryDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<DeliveryListResponseDto>> GetDeliveriesAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _getDeliveriesResult ?? ServiceResult<DeliveryListResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<DeliveryDetailDto>> GetDeliveryDetailAsync(
            Guid orderId,
            Guid deliveryId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _getDeliveryDetailResult ?? ServiceResult<DeliveryDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<DeliveryBatchCompletionDto>> CompleteDeliveryBatchAsync(
            Guid orderId,
            Guid deliveryId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _completeDeliveryBatchResult ?? ServiceResult<DeliveryBatchCompletionDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderDeliveryTrackingDto>> GetDeliveryTrackingAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _getDeliveryTrackingResult ?? ServiceResult<OrderDeliveryTrackingDto>.Unauthorized());
        }
    }

    private sealed class FakePaymentService : IPaymentService
    {
        private readonly ServiceResult<PaymentDetailDto>? _createDepositResult;
        private readonly ServiceResult<PaymentDetailDto>? _createRemainingResult;

        public FakePaymentService(
            ServiceResult<PaymentDetailDto>? createDepositResult = null,
            ServiceResult<PaymentDetailDto>? createRemainingResult = null)
        {
            _createDepositResult = createDepositResult;
            _createRemainingResult = createRemainingResult;
        }

        public Task<ServiceResult<PaymentDetailDto>> CreateDepositPaymentForOrderAsync(
            Guid orderId,
            Guid currentUserId,
            CreateOrderDepositPaymentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _createDepositResult ?? ServiceResult<PaymentDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<PaymentDetailDto>> CreateRemainingPaymentForOrderAsync(
            Guid orderId,
            Guid currentUserId,
            CreateOrderRemainingPaymentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _createRemainingResult ?? ServiceResult<PaymentDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<PaymentDetailDto>> CreateProjectStartFeePaymentAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProjectStartFeePaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<ProjectStartFeeStatusDto>> GetProjectStartFeeStatusAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectStartFeeStatusDto>.Unauthorized());

        public Task<ServiceResult<PaymentDetailDto>> GetByIdAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<PaymentListResponseDto>> GetListAsync(
            Guid currentUserId,
            PaymentQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentListResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentTransactionListResponseDto>> GetTransactionsAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentTransactionListResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentStatusByCodeDto>> GetStatusByCodeAsync(
            string paymentCode,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentStatusByCodeDto>.Unauthorized());

        public Task<ServiceResult<SePayVietQrResponseDto>> GenerateSePayVietQrAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<SePayVietQrResponseDto>.Unauthorized());

        public Task<bool> CanAccessPaymentAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<ServiceResult<PayOsPaymentLinkResponseDto>> CreatePayOsPaymentLinkAsync(
            Guid paymentId,
            Guid currentUserId,
            CreatePayOsPaymentLinkRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PayOsPaymentLinkResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentTransactionAttemptResponseDto>> CreatePaymentTransactionAttemptAsync(
            Guid paymentId,
            Guid currentUserId,
            CreatePaymentTransactionAttemptRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentTransactionAttemptResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentSummaryResponseDto>> GetSummaryAsync(
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentSummaryResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentTransactionDto?>> GetActiveTransactionAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentTransactionDto?>.Unauthorized());

        public Task<ServiceResult<PaymentTransactionDto>> CancelTransactionAsync(
            Guid paymentId,
            Guid paymentTransactionId,
            Guid currentUserId,
            CancelPaymentTransactionRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentTransactionDto>.Unauthorized());

        public Task<ServiceResult<PayOsConfirmWebhookResponseDto>> ConfirmPayOsWebhookAsync(
            PayOsConfirmWebhookRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PayOsConfirmWebhookResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentDetailDto>> CreateTestPaymentAsync(
            Guid currentUserId,
            CreateTestPaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());
    }
}
