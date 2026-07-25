#nullable enable

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
    public void UpdateFinancialAdjustment_RequiresSalesOrAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.UpdateFinancialAdjustment));

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

    [Theory]
    [InlineData(nameof(OrdersController.CreateAdjustment))]
    [InlineData(nameof(OrdersController.AddAdjustmentItem))]
    [InlineData(nameof(OrdersController.UpdateAdjustmentItem))]
    [InlineData(nameof(OrdersController.DeleteAdjustmentItem))]
    public void AdjustmentActions_RequireSalesOrAdmin(string methodName)
    {
        var authorize = GetMethodAuthorizeAttribute(methodName);

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void ConfirmAdjustment_RequiresCustomer()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.ConfirmAdjustment));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER", authorize.Roles);
    }

    [Fact]
    public void StartDelivery_RequiresSalesProductionOrAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.StartDelivery));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public void UpdateDeliveredQuantity_RequiresSalesProductionOrAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.UpdateDeliveredQuantity));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public void ConfirmItemDelivery_RequiresCustomer()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(OrdersController.ConfirmItemDelivery));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER", authorize.Roles);
    }

    [Fact]
    public async Task UpdateFinancialAdjustment_ReturnsServiceResult()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            updateFinancialAdjustmentResult: ServiceResult<OrderDetailDto>.Success(
                new OrderDetailDto { OrderId = orderId, FinalTotalAmount = 95_000_000m },
                "updated"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.UpdateFinancialAdjustment(
            orderId,
            new UpdateOrderFinancialAdjustmentRequestDto
            {
                AdditionalDiscountAmount = 5_000_000m,
                DepositAmount = 25_000_000m
            });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
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
    public async Task AdjustmentActions_ReturnServiceResultAndPassRequest()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var adjustmentId = Guid.NewGuid();
        var adjustmentItemId = Guid.NewGuid();
        var createRequest = new CreateOrderAdjustmentDto { Reason = "reason" };
        var itemRequest = new UpsertOrderAdjustmentItemDto
        {
            AdjustmentType = OrderAdjustmentItemType.ADDITIONAL_DISCOUNT,
            AdjustmentAmount = 500_000m,
            Reason = "discount"
        };
        var orderService = new FakeOrderService(
            createAdjustmentResult: ServiceResult<OrderAdjustmentDto>.Created(
                new OrderAdjustmentDto { OrderAdjustmentId = adjustmentId },
                "created"),
            addAdjustmentItemResult: ServiceResult<OrderAdjustmentItemDto>.Created(
                new OrderAdjustmentItemDto { OrderAdjustmentItemId = adjustmentItemId },
                "created"),
            updateAdjustmentItemResult: ServiceResult<OrderAdjustmentItemDto>.Success(
                new OrderAdjustmentItemDto { OrderAdjustmentItemId = adjustmentItemId },
                "updated"),
            deleteAdjustmentItemResult: ServiceResult<OrderAdjustmentDto>.Success(
                new OrderAdjustmentDto { OrderAdjustmentId = adjustmentId },
                "deleted"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var create = await controller.CreateAdjustment(orderId, createRequest);
        var add = await controller.AddAdjustmentItem(adjustmentId, itemRequest);
        var update = await controller.UpdateAdjustmentItem(adjustmentItemId, itemRequest);
        var delete = await controller.DeleteAdjustmentItem(adjustmentItemId);

        Assert.Equal(201, Assert.IsType<ObjectResult>(create).StatusCode);
        Assert.Equal(201, Assert.IsType<ObjectResult>(add).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(update).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(delete).StatusCode);
        Assert.Equal(orderId, orderService.OrderId);
        Assert.Equal(userId, orderService.CurrentUserId);
        Assert.Equal(adjustmentId, orderService.OrderAdjustmentId);
        Assert.Equal(adjustmentItemId, orderService.OrderAdjustmentItemId);
        Assert.Same(createRequest, orderService.CreateAdjustmentRequest);
        Assert.Same(itemRequest, orderService.UpsertAdjustmentItemRequest);
    }

    [Fact]
    public async Task AdjustmentActions_WithoutUser_ReturnUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var create = await controller.CreateAdjustment(Guid.NewGuid(), new CreateOrderAdjustmentDto());
        var add = await controller.AddAdjustmentItem(Guid.NewGuid(), new UpsertOrderAdjustmentItemDto());
        var update = await controller.UpdateAdjustmentItem(Guid.NewGuid(), new UpsertOrderAdjustmentItemDto());
        var delete = await controller.DeleteAdjustmentItem(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(create);
        Assert.IsType<UnauthorizedResult>(add);
        Assert.IsType<UnauthorizedResult>(update);
        Assert.IsType<UnauthorizedResult>(delete);
    }

    [Fact]
    public async Task ConfirmAdjustment_ReturnsServiceResult()
    {
        var userId = Guid.NewGuid();
        var adjustmentId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            confirmAdjustmentResult: ServiceResult<OrderAdjustmentConfirmationDto>.Success(
                new OrderAdjustmentConfirmationDto { OrderAdjustmentId = adjustmentId },
                "confirmed"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.ConfirmAdjustment(adjustmentId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(adjustmentId, orderService.OrderAdjustmentId);
        Assert.Equal(userId, orderService.CurrentUserId);
    }

    [Fact]
    public async Task ConfirmAdjustment_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.ConfirmAdjustment(Guid.NewGuid());

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
    public async Task UpdateDeliveredQuantity_ReturnsServiceResult()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new UpdateDeliveredQuantityRequestDto { DeliveredQuantityIncrement = 2 };
        var orderService = new FakeOrderService(
            updateDeliveredQuantityResult: ServiceResult<OrderItemDeliveredQuantityDto>.Success(
                new OrderItemDeliveredQuantityDto { OrderItemId = itemId, DeliveredQuantity = 2 },
                "updated"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.UpdateDeliveredQuantity(itemId, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(itemId, orderService.OrderItemId);
        Assert.Equal(userId, orderService.CurrentUserId);
        Assert.Same(request, orderService.UpdateDeliveredQuantityRequest);
    }

    [Fact]
    public async Task UpdateDeliveredQuantity_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.UpdateDeliveredQuantity(
            Guid.NewGuid(),
            new UpdateDeliveredQuantityRequestDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ConfirmItemDelivery_ReturnsServiceResult()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var orderService = new FakeOrderService(
            confirmItemDeliveryResult: ServiceResult<OrderItemDeliveryConfirmationDto>.Success(
                new OrderItemDeliveryConfirmationDto { OrderItemId = itemId },
                "confirmed"));
        var controller = CreateController(orderService, new FakePaymentService(), new FakeProductionRequestService(), userId);

        var result = await controller.ConfirmItemDelivery(itemId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(itemId, orderService.OrderItemId);
        Assert.Equal(userId, orderService.CurrentUserId);
    }

    [Fact]
    public async Task ConfirmItemDelivery_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new FakeOrderService(),
            new FakePaymentService(),
            new FakeProductionRequestService(),
            userId: null);

        var result = await controller.ConfirmItemDelivery(Guid.NewGuid());

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

        public Task<ServiceResult<ProductionRequestStatusDto>> MarkFeasibleAsync(
            Guid productionRequestId,
            Guid currentUserId,
            MarkProductionRequestFeasibleDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestStatusDto>.Unauthorized());
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
        private readonly ServiceResult<OrderDetailDto>? _updateFinancialAdjustmentResult;
        private readonly ServiceResult<OrderAdjustmentDto>? _createAdjustmentResult;
        private readonly ServiceResult<OrderAdjustmentItemDto>? _addAdjustmentItemResult;
        private readonly ServiceResult<OrderAdjustmentItemDto>? _updateAdjustmentItemResult;
        private readonly ServiceResult<OrderAdjustmentDto>? _deleteAdjustmentItemResult;
        private readonly ServiceResult<OrderAdjustmentConfirmationDto>? _confirmAdjustmentResult;
        private readonly ServiceResult<OrderDeliveryStartDto>? _startDeliveryResult;
        private readonly ServiceResult<OrderItemDeliveredQuantityDto>? _updateDeliveredQuantityResult;
        private readonly ServiceResult<OrderItemDeliveryConfirmationDto>? _confirmItemDeliveryResult;

        public FakeOrderService(
            ServiceResult<OrderListResponseDto>? getByProjectResult = null,
            ServiceResult<OrderDetailDto>? getDetailResult = null,
            ServiceResult<OrderDetailDto>? updateFinancialAdjustmentResult = null,
            ServiceResult<OrderAdjustmentDto>? createAdjustmentResult = null,
            ServiceResult<OrderAdjustmentItemDto>? addAdjustmentItemResult = null,
            ServiceResult<OrderAdjustmentItemDto>? updateAdjustmentItemResult = null,
            ServiceResult<OrderAdjustmentDto>? deleteAdjustmentItemResult = null,
            ServiceResult<OrderAdjustmentConfirmationDto>? confirmAdjustmentResult = null,
            ServiceResult<OrderDeliveryStartDto>? startDeliveryResult = null,
            ServiceResult<OrderItemDeliveredQuantityDto>? updateDeliveredQuantityResult = null,
            ServiceResult<OrderItemDeliveryConfirmationDto>? confirmItemDeliveryResult = null)
        {
            _getByProjectResult = getByProjectResult;
            _getDetailResult = getDetailResult;
            _updateFinancialAdjustmentResult = updateFinancialAdjustmentResult;
            _createAdjustmentResult = createAdjustmentResult;
            _addAdjustmentItemResult = addAdjustmentItemResult;
            _updateAdjustmentItemResult = updateAdjustmentItemResult;
            _deleteAdjustmentItemResult = deleteAdjustmentItemResult;
            _confirmAdjustmentResult = confirmAdjustmentResult;
            _startDeliveryResult = startDeliveryResult;
            _updateDeliveredQuantityResult = updateDeliveredQuantityResult;
            _confirmItemDeliveryResult = confirmItemDeliveryResult;
        }

        public Guid OrderId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public Guid OrderAdjustmentId { get; private set; }
        public Guid OrderAdjustmentItemId { get; private set; }
        public Guid OrderItemId { get; private set; }
        public CreateOrderAdjustmentDto? CreateAdjustmentRequest { get; private set; }
        public UpsertOrderAdjustmentItemDto? UpsertAdjustmentItemRequest { get; private set; }
        public UpdateDeliveredQuantityRequestDto? UpdateDeliveredQuantityRequest { get; private set; }

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

        public Task<ServiceResult<OrderDetailDto>> UpdateFinancialAdjustmentAsync(
            Guid orderId,
            Guid currentUserId,
            UpdateOrderFinancialAdjustmentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _updateFinancialAdjustmentResult ?? ServiceResult<OrderDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderAdjustmentDto>> CreateAdjustmentAsync(
            Guid orderId,
            Guid currentUserId,
            CreateOrderAdjustmentDto request,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            CreateAdjustmentRequest = request;
            return Task.FromResult(_createAdjustmentResult ?? ServiceResult<OrderAdjustmentDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderAdjustmentItemDto>> AddAdjustmentItemAsync(
            Guid orderAdjustmentId,
            Guid currentUserId,
            UpsertOrderAdjustmentItemDto request,
            CancellationToken cancellationToken = default)
        {
            OrderAdjustmentId = orderAdjustmentId;
            CurrentUserId = currentUserId;
            UpsertAdjustmentItemRequest = request;
            return Task.FromResult(_addAdjustmentItemResult ?? ServiceResult<OrderAdjustmentItemDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderAdjustmentItemDto>> UpdateAdjustmentItemAsync(
            Guid orderAdjustmentItemId,
            Guid currentUserId,
            UpsertOrderAdjustmentItemDto request,
            CancellationToken cancellationToken = default)
        {
            OrderAdjustmentItemId = orderAdjustmentItemId;
            CurrentUserId = currentUserId;
            UpsertAdjustmentItemRequest = request;
            return Task.FromResult(_updateAdjustmentItemResult ?? ServiceResult<OrderAdjustmentItemDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderAdjustmentDto>> DeleteAdjustmentItemAsync(
            Guid orderAdjustmentItemId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderAdjustmentItemId = orderAdjustmentItemId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_deleteAdjustmentItemResult ?? ServiceResult<OrderAdjustmentDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderAdjustmentConfirmationDto>> ConfirmAdjustmentAsync(
            Guid orderAdjustmentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderAdjustmentId = orderAdjustmentId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _confirmAdjustmentResult ?? ServiceResult<OrderAdjustmentConfirmationDto>.Unauthorized());
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

        public Task<ServiceResult<OrderItemDeliveredQuantityDto>> UpdateDeliveredQuantityAsync(
            Guid orderItemId,
            Guid currentUserId,
            UpdateDeliveredQuantityRequestDto request,
            CancellationToken cancellationToken = default)
        {
            OrderItemId = orderItemId;
            CurrentUserId = currentUserId;
            UpdateDeliveredQuantityRequest = request;
            return Task.FromResult(
                _updateDeliveredQuantityResult ?? ServiceResult<OrderItemDeliveredQuantityDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderItemDeliveryConfirmationDto>> ConfirmItemDeliveryAsync(
            Guid orderItemId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            OrderItemId = orderItemId;
            CurrentUserId = currentUserId;
            return Task.FromResult(
                _confirmItemDeliveryResult ?? ServiceResult<OrderItemDeliveryConfirmationDto>.Unauthorized());
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
