#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.Orders;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderServiceTests
{
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _salesId = Guid.NewGuid();
    private readonly Guid _designerId = Guid.NewGuid();

    [Fact]
    public async Task GetByProjectAsync_WithEmptyUser_ReturnsUnauthorized()
    {
        var service = BuildService(options: null);

        var result = await service.GetByProjectAsync(_projectId, Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetByProjectAsync_WhenProjectMissing_ReturnsProjectNotFound()
    {
        var service = BuildService(new OrderServiceTestOptions { Role = "ADMIN" });

        var result = await service.GetByProjectAsync(_projectId, Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.ProjectNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetByProjectAsync_CustomerSeesOnlyAccessibleOrders()
    {
        var ownOrder = CreateListItem(OrderStatus.DEPOSIT_PENDING, _customerId);
        var otherOrder = CreateListItem(OrderStatus.DEPOSIT_PENDING, Guid.NewGuid());
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            Orders = [ownOrder, otherOrder]
        });

        var result = await service.GetByProjectAsync(_projectId, _customerId);

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(ownOrder.OrderId, item.OrderId);
    }

    [Fact]
    public async Task GetByProjectAsync_ProductionSeesProductionReadyOrdersOnly()
    {
        var visible = CreateListItem(OrderStatus.DEPOSIT_PAID, _customerId);
        var hidden = CreateListItem(OrderStatus.DEPOSIT_PENDING, _customerId);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            ProjectDetail = CreateProjectDetail(),
            Orders = [visible, hidden]
        });

        var result = await service.GetByProjectAsync(_projectId, Guid.NewGuid());

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(visible.OrderId, item.OrderId);
        Assert.Equal(OrderStatus.DEPOSIT_PAID, item.Status);
    }

    [Fact]
    public async Task GetDetailAsync_WhenOrderMissing_ReturnsOrderNotFound()
    {
        var service = BuildService(new OrderServiceTestOptions { Role = "ADMIN", ProjectDetail = CreateProjectDetail() });

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.OrderNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetDetailAsync_WhenForbidden_ReturnsForbidden()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            OrderDetail = CreateDetail(orderId, Guid.NewGuid())
        });

        var result = await service.GetDetailAsync(orderId, _customerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_WhenAuthorized_ReturnsMappedDetail()
    {
        var orderId = Guid.NewGuid();
        var detail = CreateDetail(orderId, _customerId);
        detail.Items =
        [
            new OrderItemDetailReadModel
            {
                OrderItemId = Guid.NewGuid(),
                ItemName = "Counter",
                Quantity = 1,
                Status = OrderItemStatus.DELIVERED,
                DeliveredAt = DateTime.UtcNow,
                DeliveredBy = _salesId,
                UnitPrice = 100m,
                SubtotalAmount = 100m
            }
        ];
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            OrderDetail = detail
        });

        var result = await service.GetDetailAsync(orderId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(orderId, result.Data!.OrderId);
        Assert.Equal("ORD-001", result.Data.OrderCode);
        Assert.Single(result.Data.Items);
        Assert.Equal("Counter", result.Data.Items[0].ItemName);
        Assert.Equal(OrderItemStatus.DELIVERED, result.Data.Items[0].Status);
        Assert.NotNull(result.Data.Items[0].DeliveredAt);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenAnyDeliverableItemNotReady_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.READY_FOR_DELIVERY
        };
        var readyItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 1
        };
        var notReadyDeliverableItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.DELIVERED,
            Quantity = 1
        };
        var pendingFeeItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.PENDING,
            Quantity = 1
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.READY_FOR_DELIVERY
            },
            OrderItems = [readyItem, notReadyDeliverableItem, pendingFeeItem],
            HasConfirmedDeliverySchedule = true
        });

        var result = await service.StartDeliveryAsync(orderId, _salesId);

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.DeliverableItemsNotReady, result.ErrorCode);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenReady_UpdatesStatusAndNotifies()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            OrderCode = "ORD-001",
            Status = OrderStatus.READY_FOR_DELIVERY
        };
        var project = new Project
        {
            ProjectId = _projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            ProjectName = "Cafe",
            Status = ProjectStatus.READY_FOR_DELIVERY
        };
        var readyItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 1
        };
        var dispatcher = new CapturingNotificationDispatcher();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            Order = order,
            Project = project,
            OrderItems = [readyItem],
            HasConfirmedDeliverySchedule = true,
            Notifications = dispatcher
        });

        var result = await service.StartDeliveryAsync(orderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(OrderStatus.DELIVERING, order.Status);
        Assert.Equal(ProjectStatus.DELIVERING, project.Status);
        Assert.Contains(NotificationType.OrderUpdated, dispatcher.Types);
        Assert.Contains(NotificationType.ProjectStatusChanged, dispatcher.Types);
    }

    [Fact]
    public async Task CompleteDeliveryAsync_WhenValid_MarksAllReadyItemsDelivered()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            OrderCode = "ORD-001",
            Status = OrderStatus.DELIVERING
        };
        var item = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 2
        };
        var dispatcher = new CapturingNotificationDispatcher();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                ProjectName = "Cafe",
                Status = ProjectStatus.DELIVERING
            },
            Order = order,
            OrderItems = [item],
            Notifications = dispatcher
        });

        var result = await service.CompleteDeliveryAsync(orderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(OrderItemStatus.DELIVERED, item.Status);
        Assert.Equal(_salesId, item.DeliveredBy);
        Assert.NotNull(item.DeliveredAt);
        Assert.Equal(1, result.Data!.DeliveredItemCount);
        Assert.Contains(NotificationType.OrderUpdated, dispatcher.Types);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenAllItemsDelivered_MarksOrderDeliveredAndNotifies()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            OrderCode = "ORD-001",
            Status = OrderStatus.DELIVERING
        };
        var item = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.DELIVERED,
            Quantity = 1,
            DeliveredAt = DateTime.UtcNow,
            DeliveredBy = _salesId
        };
        var dispatcher = new CapturingNotificationDispatcher();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                ProjectName = "Cafe",
                Status = ProjectStatus.DELIVERING
            },
            Order = order,
            OrderItems = [item],
            Notifications = dispatcher
        });

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(OrderStatus.DELIVERED, order.Status);
        Assert.Contains(NotificationType.OrderDelivered, dispatcher.Types);
        Assert.Contains(NotificationType.ProjectStatusChanged, dispatcher.Types);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenAlreadyDelivering_ReturnsSuccessWithoutChangingStatus()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            OrderCode = "ORD-001",
            Status = OrderStatus.DELIVERING
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            }
        });

        var result = await service.StartDeliveryAsync(orderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(OrderStatus.DELIVERING, order.Status);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenAlreadyDelivered_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.DELIVERED
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERED
            }
        });

        var result = await service.StartDeliveryAsync(orderId, _salesId);

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.OrderAlreadyDelivered, result.ErrorCode);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenDeliveryScheduleNotConfirmed_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.READY_FOR_DELIVERY
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.READY_FOR_DELIVERY
            },
            HasConfirmedDeliverySchedule = false
        });

        var result = await service.StartDeliveryAsync(orderId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryScheduleNotConfirmed, result.ErrorCode);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenForbidden_ReturnsForbidden()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.READY_FOR_DELIVERY
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.READY_FOR_DELIVERY
            },
            HasConfirmedDeliverySchedule = true
        });

        var result = await service.StartDeliveryAsync(orderId, _customerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CompleteDeliveryAsync_WhenOrderNotDelivering_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.READY_FOR_DELIVERY
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.READY_FOR_DELIVERY
            }
        });

        var result = await service.CompleteDeliveryAsync(orderId, _salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.OrderNotDelivering, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteDeliveryAsync_WhenAlreadyDelivered_ReturnsSuccessWithZeroCount()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.DELIVERING
        };
        var item = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.DELIVERED,
            Quantity = 1,
            DeliveredAt = DateTime.UtcNow,
            DeliveredBy = _salesId
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            Order = order,
            OrderItems = [item],
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            }
        });

        var result = await service.CompleteDeliveryAsync(orderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(0, result.Data!.DeliveredItemCount);
    }

    [Fact]
    public async Task CompleteDeliveryAsync_WhenItemsNotReady_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.DELIVERING
        };
        var readyItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 1
        };
        var deliveredItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.DELIVERED,
            Quantity = 1,
            DeliveredAt = DateTime.UtcNow,
            DeliveredBy = _salesId
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            Order = order,
            OrderItems = [readyItem, deliveredItem],
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            }
        });

        var result = await service.CompleteDeliveryAsync(orderId, _salesId);

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.DeliverableItemsNotReady, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenItemsNotDelivered_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.DELIVERING
        };
        var item = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 1
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            Order = order,
            OrderItems = [item],
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            }
        });

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.DeliverableItemsNotDelivered, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenAlreadyConfirmed_ReturnsSuccess()
    {
        var orderId = Guid.NewGuid();
        var confirmedAt = DateTime.UtcNow.AddHours(-1);
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            OrderCode = "ORD-001",
            Status = OrderStatus.DELIVERED,
            CustomerConfirmedDeliveryAt = confirmedAt
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERED
            }
        });

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(confirmedAt, order.CustomerConfirmedDeliveryAt);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenNotDelivering_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.READY_FOR_DELIVERY
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.READY_FOR_DELIVERY
            }
        });

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.OrderNotDelivering, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenForbidden_ReturnsForbidden()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.DELIVERING
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = Guid.NewGuid(),
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            }
        });

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task PrepareFinalPaymentAsync_WhenDelivered_NotifiesOrderUpdated()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            OrderCode = "ORD-001",
            FinalTotalAmount = 100m,
            Status = OrderStatus.DELIVERED,
            CustomerConfirmedDeliveryAt = DateTime.UtcNow
        };
        var dispatcher = new CapturingNotificationDispatcher();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            OrderDetail = CreateDetail(orderId, _customerId),
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                ProjectName = "Cafe",
                Status = ProjectStatus.DELIVERED
            },
            SummedPaidAmount = 30m,
            Notifications = dispatcher
        });

        var result = await service.PrepareFinalPaymentAsync(orderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(OrderStatus.FINAL_PAYMENT_PENDING, order.Status);
        Assert.Equal(NotificationType.OrderUpdated, Assert.Single(dispatcher.Types));
    }

    [Fact]
    public async Task CompleteAsync_WhenReady_CompletesOrderAndNotifies()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            OrderCode = "ORD-001",
            FinalTotalAmount = 100m,
            Status = OrderStatus.DELIVERED,
            CustomerConfirmedDeliveryAt = DateTime.UtcNow
        };
        var item = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.DELIVERED,
            Quantity = 1,
            DeliveredAt = DateTime.UtcNow
        };
        var project = new Project
        {
            ProjectId = _projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            ProjectName = "Cafe",
            Status = ProjectStatus.DELIVERED
        };
        var dispatcher = new CapturingNotificationDispatcher();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            OrderDetail = CreateDetail(orderId, _customerId),
            Order = order,
            OrderItems = [item],
            Project = project,
            SummedPaidAmount = 100m,
            Notifications = dispatcher
        });

        var result = await service.CompleteAsync(orderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(OrderStatus.COMPLETED, order.Status);
        Assert.Equal(ProjectStatus.COMPLETED, project.Status);
        Assert.Contains(NotificationType.OrderCompleted, dispatcher.Types);
        Assert.Contains(NotificationType.ProjectStatusChanged, dispatcher.Types);
    }

    private static OrderService BuildService(OrderServiceTestOptions? options = null)
    {
        options ??= new OrderServiceTestOptions();
        var payments = new EmptyPaymentRepository { SummedPaidAmount = options.SummedPaidAmount };
        return new OrderService(
            new FakeOrderRepository(
                options.Orders,
                options.OrderDetail,
                options.Order,
                options.OrderItem,
                options.OrderItems),
            new FakeProjectRepository(options.ProjectDetail, options.Role, options.Project),
            payments,
            new EmptyProjectScheduleRepository { ConfirmedDeliverySchedule = options.HasConfirmedDeliverySchedule },
            new FakeUnitOfWork(),
            options.Notifications);
    }

    private ProjectDetailReadModel CreateProjectDetail()
    {
        return new ProjectDetailReadModel
        {
            ProjectId = _projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            AssignedDesignerId = _designerId,
            Status = ProjectStatus.ORDER_CONFIRMED
        };
    }

    private OrderListItemReadModel CreateListItem(OrderStatus status, Guid customerId)
    {
        return new OrderListItemReadModel
        {
            OrderId = Guid.NewGuid(),
            ProjectId = _projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-LIST",
            OriginalTotalAmount = 100m,
            DepositAmount = 30m,
            PaidAmount = 0m,
            RemainingAmount = 100m,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            CustomerId = customerId,
            AssignedSalesId = _salesId,
            AssignedDesignerId = _designerId
        };
    }

    private OrderDetailReadModel CreateDetail(Guid orderId, Guid customerId)
    {
        return new OrderDetailReadModel
        {
            OrderId = orderId,
            ProjectId = _projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-001",
            CustomerId = customerId,
            SalesId = _salesId,
            OriginalTotalAmount = 100m,
            FinalTotalAmount = 100m,
            DepositAmount = 30m,
            PaidAmount = 0m,
            RemainingAmount = 100m,
            Status = OrderStatus.DEPOSIT_PENDING,
            AssignedSalesId = _salesId,
            AssignedDesignerId = _designerId
        };
    }

    private sealed class OrderServiceTestOptions
    {
        public string Role { get; init; } = "ADMIN";

        public ProjectDetailReadModel? ProjectDetail { get; init; }

        public IReadOnlyList<OrderListItemReadModel> Orders { get; init; } = [];

        public OrderDetailReadModel? OrderDetail { get; init; }

        public Order? Order { get; init; }

        public OrderItem? OrderItem { get; init; }

        public IReadOnlyList<OrderItem> OrderItems { get; init; } = [];

        public Project? Project { get; init; }

        public bool HasConfirmedDeliverySchedule { get; init; }

        public decimal SummedPaidAmount { get; init; }

        public INotificationDispatcher? Notifications { get; init; }
    }

    private sealed class FakeOrderRepository(
        IReadOnlyList<OrderListItemReadModel> orders,
        OrderDetailReadModel? orderDetail,
        Order? order = null,
        OrderItem? orderItem = null,
        IReadOnlyList<OrderItem>? orderItems = null) : IOrderRepository
    {
        public Task<IReadOnlyList<OrderListItemReadModel>> GetByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            var items = orders.Where(order => order.ProjectId == projectId).ToList();
            return Task.FromResult<IReadOnlyList<OrderListItemReadModel>>(items);
        }

        public Task<OrderDetailReadModel?> GetDetailAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(orderDetail?.OrderId == orderId ? orderDetail : null);
        }

        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(order?.OrderId == orderId ? order : null);

        public Task<OrderItem?> GetItemByIdAsync(
            Guid orderItemId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(orderItem?.OrderItemId == orderItemId ? orderItem : null);

        public Task<IReadOnlyList<OrderItem>> GetItemsByOrderAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            var items = (orderItems ?? [])
                .Where(item => item.OrderId == orderId)
                .ToList();
            return Task.FromResult<IReadOnlyList<OrderItem>>(items);
        }

        public void UpdateItem(OrderItem item)
        {
        }

        public Task<bool> AllDeliverableItemsReadyAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            var items = GetDeliverableItems(orderId);
            return Task.FromResult(items.Count > 0 && items.All(item => item.Status == OrderItemStatus.READY));
        }

        public Task<bool> AllDeliverableItemsDeliveredAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            var items = GetDeliverableItems(orderId);
            return Task.FromResult(items.Count > 0 && items.All(item => item.Status == OrderItemStatus.DELIVERED));
        }

        private List<OrderItem> GetDeliverableItems(Guid orderId)
        {
            return (orderItems ?? [])
                .Where(item =>
                    item.OrderId == orderId &&
                    item.ProductVersionId.HasValue &&
                    (item.Quantity ?? 0) > 0 &&
                    item.Status is OrderItemStatus.READY or OrderItemStatus.DELIVERED)
                .ToList();
        }

        public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(Order order)
        {
        }

        public IQueryable<Order> Query() => Enumerable.Empty<Order>().AsQueryable();

        public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>([]);

        public Task AddRangeAsync(IEnumerable<Order> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Remove(Order entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeProjectRepository(
        ProjectDetailReadModel? project,
        string role,
        Project? projectEntity) : IProjectRepository
    {
        public Task<ProjectDetailReadModel?> GetDetailAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(project?.ProjectId == projectId ? project : null);
        }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(role);

        public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();

        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(projectEntity?.ProjectId == id ? projectEntity : null);

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Project>>([]);

        public Task AddAsync(Project entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(Project entity)
        {
        }

        public void Remove(Project entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(
            Guid designerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DesignerAccountReadModel?>(null);

        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
            ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);

        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectSearchIndexItemReadModel?>(null);

        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);

        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);

        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class EmptyPaymentRepository : IPaymentRepository
    {
        public decimal SummedPaidAmount { get; init; }

        public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
            => Task.FromResult<Payment?>(null);

        public Task<Infrastructure.ReadModels.Payments.PaymentDetailReadModel?> GetDetailAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Payments.PaymentDetailReadModel?>(null);

        public Task<Infrastructure.ReadModels.Payments.PaymentDetailReadModel?> GetDetailByPaymentCodeAsync(
            string paymentCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Payments.PaymentDetailReadModel?>(null);

        public Task<Infrastructure.ReadModels.Payments.PaymentStatusByCodeReadModel?> GetStatusByPaymentCodeAsync(
            string paymentCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Payments.PaymentStatusByCodeReadModel?>(null);

        public Task<IReadOnlyList<Infrastructure.ReadModels.Payments.PaymentListItemReadModel>> GetListAsync(
            Infrastructure.ReadModels.Payments.PaymentQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Payments.PaymentListItemReadModel>>([]);

        public Task<IReadOnlyList<Infrastructure.ReadModels.Payments.PaymentTransactionReadModel>> GetTransactionsByPaymentIdAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Infrastructure.ReadModels.Payments.PaymentTransactionReadModel>>([]);

        public Task<bool> PaymentCodeExistsAsync(string paymentCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> TransactionCodeExistsAsync(string transactionCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> ProviderTransactionExistsAsync(
            PaymentProvider provider,
            string providerTransactionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> PayOsOrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<PaymentTransaction?> GetTransactionByProviderReferenceAsync(
            PaymentProvider provider,
            string providerReferenceCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentTransaction?>(null);

        public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Payment?> GetByOrderAndTypeAsync(
            Guid orderId,
            PaymentType paymentType,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Payment?>(null);

        public Task<Payment?> GetByProjectAndTypeAsync(
            Guid projectId,
            PaymentType paymentType,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Payment?>(null);

        public Task<decimal> SumOrderScopedPaidAmountAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(SummedPaidAmount);

        public Task<bool> HasSuccessfulTransactionAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> CountAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.CountAsync(query, cancellationToken);

        public Task<PaymentSummaryReadModel> GetSummaryAsync(
            PaymentQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetSummaryAsync(query, utcNow, cancellationToken);

        public Task<IReadOnlyList<Payment>> GetExpiredPaymentsForSyncAsync(
            PaymentQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetExpiredPaymentsForSyncAsync(query, utcNow, cancellationToken);

        public Task<PaymentTransaction?> GetTransactionByIdAsync(
            Guid paymentTransactionId,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetTransactionByIdAsync(paymentTransactionId, cancellationToken);

        public Task<PaymentTransactionReadModel?> GetLatestPendingTransactionAsync(
            Guid paymentId,
            PaymentProvider provider,
            PaymentMethod method,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetLatestPendingTransactionAsync(
                paymentId,
                provider,
                method,
                cancellationToken);

        public Task<PaymentTransactionReadModel?> GetLatestTransactionAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetLatestTransactionAsync(paymentId, cancellationToken);

        public Task<IReadOnlySet<Guid>> GetPaymentIdsWithSuccessfulTransactionAsync(
            IReadOnlyCollection<Guid> paymentIds,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetPaymentIdsWithSuccessfulTransactionAsync(paymentIds, cancellationToken);

        public void UpdatePayment(Payment payment)
        {
        }

        public void UpdateTransaction(PaymentTransaction transaction)
        {
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CapturingNotificationDispatcher : INotificationDispatcher
    {
        public List<NotificationType> Types { get; } = [];
        public List<Guid> LastReceivers { get; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            NotificationDispatchRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            Types.Add(type);
            LastReceivers.Clear();
            LastReceivers.AddRange(receiverIds);
            return Task.CompletedTask;
        }
    }

}
