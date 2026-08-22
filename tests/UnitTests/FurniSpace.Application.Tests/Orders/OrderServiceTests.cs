#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.Orders;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
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
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _productionId = Guid.NewGuid();

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
            Role = "ADMIN",
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

        var result = await service.StartDeliveryAsync(orderId, _adminId);

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
            Role = "ADMIN",
            Order = order,
            Project = project,
            OrderItems = [readyItem],
            HasConfirmedDeliverySchedule = true,
            Notifications = dispatcher
        });

        var result = await service.StartDeliveryAsync(orderId, _adminId);

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
            Role = "ADMIN",
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

        var result = await service.CompleteDeliveryAsync(orderId, _adminId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.ProjectScheduleIdRequired, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenRemainingAmountPositive_CreatesPaymentAndNotifies()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            OrderCode = "ORD-001",
            QuotationId = Guid.NewGuid(),
            FinalTotalAmount = 100m,
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
        var options = new OrderServiceTestOptions
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
            SummedPaidAmount = 30m,
            Notifications = dispatcher
        };
        var service = BuildService(options);

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(OrderStatus.FINAL_PAYMENT_PENDING, order.Status);
        Assert.Equal(ProjectStatus.DELIVERED.ToString(), result.Data!.ProjectStatus);
        Assert.Equal(30m, order.PaidAmount);
        Assert.Equal(70m, order.RemainingAmount);
        var payment = Assert.Single(options.AddedPayments);
        Assert.Equal(PaymentType.REMAINING_PAYMENT, payment.PaymentType);
        Assert.Equal(70m, payment.Amount);
        Assert.Equal(_customerId, payment.PaidBy);
        Assert.NotNull(payment.ExpiredAt);
        Assert.Contains(NotificationType.OrderDelivered, dispatcher.Types);
        Assert.Contains(NotificationType.ProjectStatusChanged, dispatcher.Types);
        Assert.Contains(NotificationType.PaymentCreated, dispatcher.Types);
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
            Role = "ADMIN",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            }
        });

        var result = await service.StartDeliveryAsync(orderId, _adminId);

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
            Role = "ADMIN",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERED
            }
        });

        var result = await service.StartDeliveryAsync(orderId, _adminId);

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
            Role = "ADMIN",
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

        var result = await service.StartDeliveryAsync(orderId, _adminId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryScheduleNotConfirmed, result.ErrorCode);
    }

    [Fact]
    public async Task StartDeliveryAsync_WhenProductionIncomplete_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            IsProductionCompleted = false,
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                SalesId = _salesId,
                Status = OrderStatus.READY_FOR_DELIVERY
            },
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.READY_FOR_DELIVERY
            },
            HasConfirmedDeliverySchedule = true
        });

        var result = await service.StartDeliveryAsync(orderId, _adminId);

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.ProductionNotCompleted, result.ErrorCode);
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
            Role = "ADMIN",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.READY_FOR_DELIVERY
            }
        });

        var result = await service.CompleteDeliveryAsync(orderId, _adminId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.OrderNotDelivering, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteDeliveryAsync_WhenProductionIncomplete_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            IsProductionCompleted = false,
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                SalesId = _salesId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            }
        });

        var result = await service.CompleteDeliveryAsync(orderId, _adminId);

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.ProductionNotCompleted, result.ErrorCode);
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
            Role = "ADMIN",
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

        var result = await service.CompleteDeliveryAsync(orderId, _adminId);

        Assert.Equal(200, result.Status);
        Assert.Equal(0, result.Data!.DeliveredItemCount);
    }

    [Fact]
    public async Task CompleteDeliveryAsync_WhenNoEligibleItems_ReturnsConflict()
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
        var notReadyItem = new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.IN_PRODUCTION,
            Quantity = 1
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = order,
            OrderItems = [notReadyItem],
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            }
        });

        var result = await service.CompleteDeliveryAsync(orderId, _adminId);

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
    public async Task ConfirmDeliveryAsync_WhenProductionIncomplete_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            IsProductionCompleted = false,
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                SalesId = _salesId,
                Status = OrderStatus.DELIVERING
            },
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
        Assert.Equal(OrderErrorCodes.ProductionNotCompleted, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenRemainingAmountZero_CompletesOrderWithoutPayment()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            FinalTotalAmount = 100m,
            Status = OrderStatus.DELIVERING
        };
        var project = new Project
        {
            ProjectId = _projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            Status = ProjectStatus.DELIVERING
        };
        var options = new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            Order = order,
            Project = project,
            OrderItems = [CreateDeliveredOrderItem(orderId)],
            SummedPaidAmount = 100m
        };
        var service = BuildService(options);

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(OrderStatus.COMPLETED, order.Status);
        Assert.Equal(ProjectStatus.DELIVERED, project.Status);
        Assert.Empty(options.AddedPayments);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenActiveRemainingPaymentExists_ReusesPayment()
    {
        var orderId = Guid.NewGuid();
        var existingPayment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = _projectId,
            OrderId = orderId,
            PaymentCode = "FS12345678",
            PaidBy = _customerId,
            PaymentType = PaymentType.REMAINING_PAYMENT,
            Amount = 70m,
            Currency = "VND",
            Status = PaymentStatus.PENDING,
            ExpiredAt = DateTime.UtcNow.AddDays(1)
        };
        var dispatcher = new CapturingNotificationDispatcher();
        var options = new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                SalesId = _salesId,
                FinalTotalAmount = 100m,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            },
            ExistingRemainingPayment = existingPayment,
            OrderItems = [CreateDeliveredOrderItem(orderId)],
            SummedPaidAmount = 30m,
            Notifications = dispatcher
        };
        var service = BuildService(options);

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Empty(options.AddedPayments);
        Assert.Contains(NotificationType.PaymentCreated, dispatcher.Types);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenPaymentCreationFails_RollsBackTransaction()
    {
        var orderId = Guid.NewGuid();
        var unitOfWork = new FakeUnitOfWork();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                SalesId = _salesId,
                FinalTotalAmount = 100m,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            },
            OrderItems = [CreateDeliveredOrderItem(orderId)],
            SummedPaidAmount = 30m,
            ThrowOnAddPayment = true,
            UnitOfWork = unitOfWork
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmDeliveryAsync(orderId, _customerId));

        Assert.True(unitOfWork.BeganTransaction);
        Assert.True(unitOfWork.RolledBackTransaction);
        Assert.False(unitOfWork.CommittedTransaction);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
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
            Role = "ADMIN",
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

        var result = await service.PrepareFinalPaymentAsync(orderId, _adminId);

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
        Assert.Equal(ProjectStatus.DELIVERED, project.Status);
        Assert.Contains(NotificationType.OrderCompleted, dispatcher.Types);
        Assert.DoesNotContain(NotificationType.ProjectStatusChanged, dispatcher.Types);
    }

    [Fact]
    public async Task CompleteAsync_WhenAlreadyCompleted_ReturnsSuccessIdempotently()
    {
        var orderId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow.AddHours(-2);
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            OrderCode = "ORD-001",
            FinalTotalAmount = 100m,
            Status = OrderStatus.COMPLETED,
            CustomerConfirmedDeliveryAt = DateTime.UtcNow,
            UpdatedAt = completedAt
        };
        var project = new Project
        {
            ProjectId = _projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            ProjectName = "Cafe",
            Status = ProjectStatus.DELIVERED
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            OrderDetail = CreateDetail(orderId, _customerId),
            Order = order,
            Project = project,
            SummedPaidAmount = 100m
        });

        var result = await service.CompleteAsync(orderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(OrderStatus.COMPLETED, order.Status);
        Assert.Equal(ProjectStatus.DELIVERED, project.Status);
        Assert.Equal(completedAt, order.UpdatedAt);
    }

    [Fact]
    public async Task CompleteAsync_WhenAlreadyCompletedWithoutUpdatedAt_ReturnsSuccess()
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
            Status = OrderStatus.COMPLETED,
            CustomerConfirmedDeliveryAt = DateTime.UtcNow,
            UpdatedAt = null
        };
        var project = new Project
        {
            ProjectId = _projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            ProjectName = "Cafe",
            Status = ProjectStatus.DELIVERED
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            OrderDetail = CreateDetail(orderId, _customerId),
            Order = order,
            Project = project,
            SummedPaidAmount = 100m
        });

        var result = await service.CompleteAsync(orderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data?.CompletedAt);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenValid_CreatesInProgressBatch()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
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
            OrderItemId = orderItemId,
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 4,
            DeliveredQuantity = 0
        };
        var deliveries = new FakeDeliveryRepository();
        deliveries.SetCreateDetailFactory(id => new DeliveryDetailReadModel
        {
            DeliveryId = id,
            OrderId = orderId,
            Status = DeliveryStatus.IN_PROGRESS,
            ItemCount = 1
        });
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            },
            OrderItems = [item],
            Deliveries = deliveries,
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _productionId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items =
                [
                    new CreateDeliveryBatchItemRequestDto
                    {
                        OrderItemId = orderItemId,
                        Quantity = 2
                    }
                ]
            });

        Assert.Equal(201, result.Status);
        Assert.Equal(DeliveryStatus.IN_PROGRESS, result.Data!.Status);
        Assert.Single(deliveries.AddedDeliveries);
        Assert.Equal(scheduleId, deliveries.AddedDeliveries[0].ProjectScheduleId);
    }

    [Fact]
    public async Task GetDeliveriesAsync_WhenAuthorized_ReturnsDeliveryList()
    {
        var orderId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var deliveries = new FakeDeliveryRepository();
        deliveries.SeedListItem(deliveryId, orderId, DeliveryStatus.COMPLETED, itemCount: 2);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "SALES",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                SalesId = _salesId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            },
            ProjectDetail = CreateProjectDetail(),
            Deliveries = deliveries
        });

        var result = await service.GetDeliveriesAsync(orderId, _salesId);

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(deliveryId, result.Data.Items[0].DeliveryId);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenQuantityExceedsRemaining_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
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
            OrderItemId = orderItemId,
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 5,
            DeliveredQuantity = 3
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            },
            OrderItems = [item],
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _productionId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items =
                [
                    new CreateDeliveryBatchItemRequestDto
                    {
                        OrderItemId = orderItemId,
                        Quantity = 3
                    }
                ]
            });

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.InvalidDeliveryQuantity, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteDeliveryBatchAsync_WhenValid_UpdatesDeliveredQuantity()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (_, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
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
            OrderItemId = orderItemId,
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 5,
            DeliveredQuantity = 0
        };
        var deliveries = new FakeDeliveryRepository();
        deliveries.SeedDelivery(
            deliveryId,
            orderId,
            DeliveryStatus.IN_PROGRESS,
            [new DeliveryItem
            {
                DeliveryItemId = Guid.NewGuid(),
                DeliveryId = deliveryId,
                OrderItemId = orderItemId,
                Quantity = 2
            }]);
        deliveries.GetDelivery(deliveryId)!.ProjectScheduleId = scheduleId;
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            },
            OrderItems = [item],
            Deliveries = deliveries,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CompleteDeliveryBatchAsync(orderId, deliveryId, _productionId);

        Assert.Equal(200, result.Status);
        Assert.Equal(2, item.DeliveredQuantity);
        Assert.Equal(OrderItemStatus.PARTIALLY_DELIVERED, item.Status);
        Assert.Equal(DeliveryStatus.COMPLETED, deliveries.GetDelivery(deliveryId)!.Status);
        Assert.Equal(ProjectScheduleStatus.COMPLETED, scheduleEntity.Status);
    }

    [Fact]
    public async Task GetDeliveryTrackingAsync_WhenAuthorized_ReturnsTracking()
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
        var tracking = new OrderDeliveryTrackingReadModel
        {
            OrderId = orderId,
            OrderStatus = OrderStatus.DELIVERING,
            TotalOrderedQuantity = 10,
            TotalDeliveredQuantity = 4,
            RemainingQuantity = 6,
            DeliveryProgressPercent = 40,
            CompletedDeliveryCount = 1,
            UpcomingDeliveryCount = 1
        };
        var deliveries = new FakeDeliveryRepository { Tracking = tracking };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            Order = order,
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                AssignedSalesId = _salesId,
                Status = ProjectStatus.DELIVERING
            },
            Deliveries = deliveries
        });

        var result = await service.GetDeliveryTrackingAsync(orderId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(orderId, result.Data!.OrderId);
        Assert.Equal(40, result.Data.Summary.DeliveryProgressPercent);
        Assert.Equal(6, result.Data.Summary.RemainingQuantity);
    }

    [Fact]
    public async Task CompleteDeliveryBatchAsync_WhenAlreadyCompleted_ReturnsSuccessWithoutMutation()
    {
        var orderId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var deliveries = new FakeDeliveryRepository();
        deliveries.SeedDelivery(
            deliveryId,
            orderId,
            DeliveryStatus.COMPLETED,
            [new DeliveryItem
            {
                DeliveryItemId = Guid.NewGuid(),
                DeliveryId = deliveryId,
                OrderItemId = Guid.NewGuid(),
                Quantity = 1
            }]);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = ProjectStatus.DELIVERING
            },
            Deliveries = deliveries,
            IsProductionCompleted = true
        });

        var result = await service.CompleteDeliveryBatchAsync(orderId, deliveryId, _adminId);

        Assert.Equal(200, result.Status);
        Assert.Equal(DeliveryStatus.COMPLETED, deliveries.GetDelivery(deliveryId)!.Status);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenFirstBatch_TransitionsOrderAndProjectToDelivering()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
        var project = new Project
        {
            ProjectId = _projectId,
            CustomerId = _customerId,
            AssignedSalesId = _salesId,
            Status = ProjectStatus.READY_FOR_DELIVERY
        };
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            SalesId = _salesId,
            Status = OrderStatus.READY_FOR_DELIVERY
        };
        var item = new OrderItem
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 3,
            DeliveredQuantity = 0
        };
        var deliveries = new FakeDeliveryRepository();
        deliveries.SetCreateDetailFactory(id => new DeliveryDetailReadModel
        {
            DeliveryId = id,
            OrderId = orderId,
            Status = DeliveryStatus.IN_PROGRESS,
            ItemCount = 1
        });
        var notifications = new CapturingNotificationDispatcher();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            Order = order,
            Project = project,
            OrderItems = [item],
            Deliveries = deliveries,
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity,
            Notifications = notifications
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _productionId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items =
                [
                    new CreateDeliveryBatchItemRequestDto
                    {
                        OrderItemId = orderItemId,
                        Quantity = 1
                    }
                ]
            });

        Assert.Equal(201, result.Status);
        Assert.Equal(OrderStatus.DELIVERING, order.Status);
        Assert.Equal(ProjectStatus.DELIVERING, project.Status);
        Assert.Equal(2, notifications.Types.Count);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenScheduleNotConfirmed_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var scheduleDetail = new ProjectScheduleDetailReadModel
        {
            ScheduleId = scheduleId,
            ProjectId = _projectId,
            ProjectName = "Test Project",
            CustomerId = _customerId,
            ScheduleType = ProjectScheduleType.DELIVERY,
            Status = ProjectScheduleStatus.PENDING_CONFIRMATION,
            AssignedStaffId = _productionId,
            ScheduledStart = DateTime.UtcNow.AddMinutes(-1)
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, CustomerId = _customerId, Status = ProjectStatus.DELIVERING },
            ScheduleDetail = scheduleDetail
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _productionId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = Guid.NewGuid(), Quantity = 1 }]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryScheduleNotConfirmed, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenScheduleAlreadyUsed_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
        var deliveries = new FakeDeliveryRepository();
        var existingDeliveryId = Guid.NewGuid();
        deliveries.SeedDelivery(
            existingDeliveryId,
            orderId,
            DeliveryStatus.COMPLETED,
            [new DeliveryItem
            {
                DeliveryItemId = Guid.NewGuid(),
                DeliveryId = existingDeliveryId,
                OrderItemId = orderItemId,
                Quantity = 1
            }]);
        deliveries.GetDelivery(existingDeliveryId)!.ProjectScheduleId = scheduleId;
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, CustomerId = _customerId, Status = ProjectStatus.DELIVERING },
            OrderItems =
            [
                new OrderItem
                {
                    OrderItemId = orderItemId,
                    OrderId = orderId,
                    ProductVersionId = Guid.NewGuid(),
                    Status = OrderItemStatus.READY,
                    Quantity = 5,
                    DeliveredQuantity = 1
                }
            ],
            Deliveries = deliveries,
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _productionId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = orderItemId, Quantity = 1 }]
            });

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryScheduleAlreadyUsed, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenDuplicateItems_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, CustomerId = _customerId, Status = ProjectStatus.DELIVERING },
            OrderItems =
            [
                new OrderItem
                {
                    OrderItemId = orderItemId,
                    OrderId = orderId,
                    ProductVersionId = Guid.NewGuid(),
                    Status = OrderItemStatus.READY,
                    Quantity = 5
                }
            ],
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _productionId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items =
                [
                    new CreateDeliveryBatchItemRequestDto { OrderItemId = orderItemId, Quantity = 1 },
                    new CreateDeliveryBatchItemRequestDto { OrderItemId = orderItemId, Quantity = 2 }
                ]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DuplicateOrderItemInBatch, result.ErrorCode);
    }

    [Fact]
    public async Task GetDeliveryDetailAsync_WhenFound_ReturnsDetail()
    {
        var orderId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var deliveries = new FakeDeliveryRepository();
        deliveries.SeedListItem(deliveryId, orderId, DeliveryStatus.IN_PROGRESS, itemCount: 1);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            },
            Deliveries = deliveries
        });

        var result = await service.GetDeliveryDetailAsync(orderId, deliveryId, _customerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(deliveryId, result.Data!.DeliveryId);
    }

    [Fact]
    public async Task GetDeliveryDetailAsync_WhenNotFound_ReturnsNotFound()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            ProjectDetail = CreateProjectDetail(),
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            }
        });

        var result = await service.GetDeliveryDetailAsync(orderId, Guid.NewGuid(), _adminId);

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetDeliveryTrackingAsync_WhenUserMissing_ReturnsUnauthorized()
    {
        var service = BuildService(new OrderServiceTestOptions { Role = "CUSTOMER" });

        var result = await service.GetDeliveryTrackingAsync(Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetDeliveryTrackingAsync_WhenForbidden_ReturnsForbidden()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            }
        });

        var result = await service.GetDeliveryTrackingAsync(orderId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenProjectScheduleIdMissing_ReturnsBadRequest()
    {
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            Order = new Order
            {
                OrderId = Guid.NewGuid(),
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING }
        });

        var result = await service.CreateDeliveryBatchAsync(
            Guid.NewGuid(),
            _productionId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = Guid.Empty,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = Guid.NewGuid(), Quantity = 1 }]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.ProjectScheduleIdRequired, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenItemsEmpty_ReturnsBadRequest()
    {
        var service = BuildService(new OrderServiceTestOptions { Role = "PRODUCTION" });

        var result = await service.CreateDeliveryBatchAsync(
            Guid.NewGuid(),
            _productionId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = Guid.NewGuid(),
                Items = []
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryBatchEmpty, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenScheduleNotStarted_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var scheduleDetail = new ProjectScheduleDetailReadModel
        {
            ScheduleId = scheduleId,
            ProjectId = _projectId,
            ProjectName = "Test",
            CustomerId = _customerId,
            ScheduleType = ProjectScheduleType.DELIVERY,
            Status = ProjectScheduleStatus.CONFIRMED,
            AssignedStaffId = _productionId,
            ScheduledStart = DateTime.UtcNow.AddDays(1)
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            ScheduleDetail = scheduleDetail
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _productionId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = Guid.NewGuid(), Quantity = 1 }]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryScheduleNotStarted, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenStaffNotAssigned_ReturnsForbidden()
    {
        var orderId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            Guid.NewGuid(),
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = Guid.NewGuid(), Quantity = 1 }]
            });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CompleteDeliveryBatchAsync_WhenOrderNotDelivering_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.READY_FOR_DELIVERY
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.READY_FOR_DELIVERY }
        });

        var result = await service.CompleteDeliveryBatchAsync(orderId, Guid.NewGuid(), _adminId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.OrderNotDelivering, result.ErrorCode);
    }

    [Fact]
    public async Task GetDeliveryTrackingAsync_WhenOrderMissing_ReturnsNotFound()
    {
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            ProjectDetail = CreateProjectDetail()
        });

        var result = await service.GetDeliveryTrackingAsync(Guid.NewGuid(), _adminId);

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.OrderNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetDeliveriesAsync_WhenUnauthorized_ReturnsUnauthorized()
    {
        var service = BuildService(new OrderServiceTestOptions { Role = "SALES" });

        var result = await service.GetDeliveriesAsync(Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task CompleteDeliveryBatchAsync_WhenAllItemsDelivered_CancelsUnusedSchedules()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var unusedScheduleId = Guid.NewGuid();
        var (_, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
        var unusedSchedule = new ProjectSchedule
        {
            ScheduleId = unusedScheduleId,
            ProjectId = _projectId,
            ScheduleType = ProjectScheduleType.DELIVERY,
            Status = ProjectScheduleStatus.CONFIRMED,
            ScheduledStart = DateTime.UtcNow.AddDays(3),
            Title = "Future delivery"
        };
        var scheduleRepo = new EmptyProjectScheduleRepository
        {
            ScheduleEntity = scheduleEntity,
            UnusedFutureDeliverySchedules = [unusedSchedule]
        };
        var order = new Order
        {
            OrderId = orderId,
            ProjectId = _projectId,
            CustomerId = _customerId,
            Status = OrderStatus.DELIVERING
        };
        var item = new OrderItem
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 2,
            DeliveredQuantity = 0
        };
        var deliveries = new FakeDeliveryRepository();
        deliveries.SeedDelivery(
            deliveryId,
            orderId,
            DeliveryStatus.IN_PROGRESS,
            [new DeliveryItem
            {
                DeliveryItemId = Guid.NewGuid(),
                DeliveryId = deliveryId,
                OrderItemId = orderItemId,
                Quantity = 2
            }]);
        deliveries.GetDelivery(deliveryId)!.ProjectScheduleId = scheduleId;
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = order,
            Project = new Project { ProjectId = _projectId, CustomerId = _customerId, Status = ProjectStatus.DELIVERING },
            OrderItems = [item],
            Deliveries = deliveries,
            Schedules = scheduleRepo
        });

        var result = await service.CompleteDeliveryBatchAsync(orderId, deliveryId, _adminId);

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectScheduleStatus.CANCELLED, unusedSchedule.Status);
        Assert.Equal("ALL_ITEMS_ALREADY_DELIVERED", unusedSchedule.InternalNote);
        Assert.Contains(scheduleRepo.UpdatedSchedules, schedule => schedule.ScheduleId == unusedScheduleId);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenProductionNotCompleted_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            IsProductionCompleted = false,
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _adminId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = Guid.NewGuid(), Quantity = 1 }]
            });

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.ProductionNotCompleted, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenInvalidOrderStatus_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DEPOSIT_PAID
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.ORDER_CONFIRMED },
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _adminId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = Guid.NewGuid(), Quantity = 1 }]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.InvalidOrderStatus, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenScheduleNotFound_ReturnsNotFound()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            OrderItems =
            [
                new OrderItem
                {
                    OrderItemId = orderItemId,
                    OrderId = orderId,
                    ProductVersionId = Guid.NewGuid(),
                    Status = OrderItemStatus.READY,
                    Quantity = 2
                }
            ]
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _adminId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = Guid.NewGuid(),
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = orderItemId, Quantity = 1 }]
            });

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryScheduleInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenScheduleWrongType_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var scheduleDetail = new ProjectScheduleDetailReadModel
        {
            ScheduleId = scheduleId,
            ProjectId = _projectId,
            ScheduleType = ProjectScheduleType.MEASUREMENT,
            Status = ProjectScheduleStatus.CONFIRMED,
            AssignedStaffId = _productionId,
            ScheduledStart = DateTime.UtcNow.AddMinutes(-1)
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            OrderItems =
            [
                new OrderItem
                {
                    OrderItemId = orderItemId,
                    OrderId = orderId,
                    ProductVersionId = Guid.NewGuid(),
                    Status = OrderItemStatus.READY,
                    Quantity = 2
                }
            ],
            ScheduleDetail = scheduleDetail
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _adminId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = orderItemId, Quantity = 1 }]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryScheduleInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenQuantityNotPositive_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _adminId);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            OrderItems =
            [
                new OrderItem
                {
                    OrderItemId = orderItemId,
                    OrderId = orderId,
                    ProductVersionId = Guid.NewGuid(),
                    Status = OrderItemStatus.READY,
                    Quantity = 2
                }
            ],
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _adminId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = orderItemId, Quantity = 0 }]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.InvalidDeliveryQuantity, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenOrderItemMissing_ReturnsNotFound()
    {
        var orderId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _adminId);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            OrderItems = [],
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _adminId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = Guid.NewGuid(), Quantity = 1 }]
            });

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.OrderItemNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenItemNotDeliverable_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _adminId);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            OrderItems =
            [
                new OrderItem
                {
                    OrderItemId = orderItemId,
                    OrderId = orderId,
                    ProductVersionId = Guid.NewGuid(),
                    Status = OrderItemStatus.CANCELLED,
                    Quantity = 2
                }
            ],
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _adminId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = orderItemId, Quantity = 1 }]
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.OrderItemNotDeliverable, result.ErrorCode);
    }

    [Fact]
    public async Task CreateDeliveryBatchAsync_WhenProductionNotAssignedToCompletedProject_ReturnsForbidden()
    {
        var orderId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (scheduleDetail, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            HasAssignedCompletedProduction = false,
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            ScheduleDetail = scheduleDetail,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CreateDeliveryBatchAsync(
            orderId,
            _productionId,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items = [new CreateDeliveryBatchItemRequestDto { OrderItemId = Guid.NewGuid(), Quantity = 1 }]
            });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CompleteDeliveryBatchAsync_WhenProductionNotCompleted_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            IsProductionCompleted = false,
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING }
        });

        var result = await service.CompleteDeliveryBatchAsync(orderId, deliveryId, _adminId);

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.ProductionNotCompleted, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteDeliveryBatchAsync_WhenDeliveryNotFound_ReturnsNotFound()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING }
        });

        var result = await service.CompleteDeliveryBatchAsync(orderId, Guid.NewGuid(), _adminId);

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteDeliveryBatchAsync_WhenNoLinkedSchedule_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var deliveries = new FakeDeliveryRepository();
        deliveries.SeedDelivery(
            deliveryId,
            orderId,
            DeliveryStatus.IN_PROGRESS,
            [new DeliveryItem
            {
                DeliveryItemId = Guid.NewGuid(),
                DeliveryId = deliveryId,
                OrderItemId = Guid.NewGuid(),
                Quantity = 1
            }]);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            Deliveries = deliveries
        });

        var result = await service.CompleteDeliveryBatchAsync(orderId, deliveryId, _adminId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryScheduleInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteDeliveryBatchAsync_WhenInvalidLinkedSchedule_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var deliveries = new FakeDeliveryRepository();
        deliveries.SeedDelivery(
            deliveryId,
            orderId,
            DeliveryStatus.IN_PROGRESS,
            [new DeliveryItem
            {
                DeliveryItemId = Guid.NewGuid(),
                DeliveryId = deliveryId,
                OrderItemId = orderItemId,
                Quantity = 1
            }]);
        deliveries.GetDelivery(deliveryId)!.ProjectScheduleId = scheduleId;
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            OrderItems =
            [
                new OrderItem
                {
                    OrderItemId = orderItemId,
                    OrderId = orderId,
                    ProductVersionId = Guid.NewGuid(),
                    Status = OrderItemStatus.READY,
                    Quantity = 2
                }
            ],
            Deliveries = deliveries,
            ScheduleEntity = new ProjectSchedule
            {
                ScheduleId = scheduleId,
                ProjectId = _projectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Status = ProjectScheduleStatus.PENDING_CONFIRMATION
            }
        });

        var result = await service.CompleteDeliveryBatchAsync(orderId, deliveryId, _adminId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryScheduleInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteDeliveryBatchAsync_WhenBatchHasNoItems_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (_, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
        var deliveries = new FakeDeliveryRepository();
        deliveries.SeedDelivery(deliveryId, orderId, DeliveryStatus.IN_PROGRESS, []);
        deliveries.GetDelivery(deliveryId)!.ProjectScheduleId = scheduleId;
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            Deliveries = deliveries,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CompleteDeliveryBatchAsync(orderId, deliveryId, _adminId);

        Assert.Equal(400, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryBatchEmpty, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteDeliveryBatchAsync_WhenFullyDelivered_SetsDeliveredAt()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var (_, scheduleEntity) = CreateConfirmedDeliverySchedule(_projectId, scheduleId, _productionId);
        var item = new OrderItem
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.READY,
            Quantity = 2,
            DeliveredQuantity = 0
        };
        var deliveries = new FakeDeliveryRepository();
        deliveries.SeedDelivery(
            deliveryId,
            orderId,
            DeliveryStatus.IN_PROGRESS,
            [new DeliveryItem
            {
                DeliveryItemId = Guid.NewGuid(),
                DeliveryId = deliveryId,
                OrderItemId = orderItemId,
                Quantity = 2
            }]);
        deliveries.GetDelivery(deliveryId)!.ProjectScheduleId = scheduleId;
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project { ProjectId = _projectId, Status = ProjectStatus.DELIVERING },
            OrderItems = [item],
            Deliveries = deliveries,
            ScheduleEntity = scheduleEntity
        });

        var result = await service.CompleteDeliveryBatchAsync(orderId, deliveryId, _adminId);

        Assert.Equal(200, result.Status);
        Assert.Equal(2, item.DeliveredQuantity);
        Assert.Equal(OrderItemStatus.READY, item.Status);
        Assert.NotNull(item.DeliveredAt);
        Assert.Equal(_adminId, item.DeliveredBy);
    }

    [Fact]
    public async Task GetDeliveryTrackingAsync_WhenTrackingMissing_ReturnsNotFound()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "ADMIN",
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            },
            ProjectDetail = CreateProjectDetail(),
            Deliveries = new FakeDeliveryRepository { Tracking = null }
        });

        var result = await service.GetDeliveryTrackingAsync(orderId, _adminId);

        Assert.Equal(404, result.Status);
        Assert.Equal(OrderErrorCodes.OrderNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetDeliveryTrackingAsync_WhenProductionStaffAssigned_ReturnsTracking()
    {
        var orderId = Guid.NewGuid();
        var tracking = new OrderDeliveryTrackingReadModel
        {
            OrderId = orderId,
            OrderStatus = OrderStatus.DELIVERING,
            TotalOrderedQuantity = 4,
            TotalDeliveredQuantity = 1,
            RemainingQuantity = 3
        };
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "PRODUCTION",
            HasAssignedCompletedProduction = true,
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            },
            ProjectDetail = CreateProjectDetail(),
            Deliveries = new FakeDeliveryRepository { Tracking = tracking }
        });

        var result = await service.GetDeliveryTrackingAsync(orderId, _productionId);

        Assert.Equal(200, result.Status);
        Assert.Equal(3, result.Data!.Summary.RemainingQuantity);
    }

    [Fact]
    public async Task GetDeliveryDetailAsync_WhenUnauthorized_ReturnsUnauthorized()
    {
        var service = BuildService(new OrderServiceTestOptions { Role = "ADMIN" });

        var result = await service.GetDeliveryDetailAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenBatchInProgress_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var deliveries = new FakeDeliveryRepository();
        deliveries.SeedDelivery(
            Guid.NewGuid(),
            orderId,
            DeliveryStatus.IN_PROGRESS,
            [new DeliveryItem
            {
                DeliveryItemId = Guid.NewGuid(),
                DeliveryId = Guid.NewGuid(),
                OrderItemId = Guid.NewGuid(),
                Quantity = 1
            }]);
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = ProjectStatus.DELIVERING
            },
            Deliveries = deliveries
        });

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.DeliveryBatchInProgress, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenUnresolvedSchedule_ReturnsConflict()
    {
        var orderId = Guid.NewGuid();
        var service = BuildService(new OrderServiceTestOptions
        {
            Role = "CUSTOMER",
            ProjectDetail = CreateProjectDetail(),
            Order = new Order
            {
                OrderId = orderId,
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = OrderStatus.DELIVERING
            },
            Project = new Project
            {
                ProjectId = _projectId,
                CustomerId = _customerId,
                Status = ProjectStatus.DELIVERING
            },
            HasUnresolvedConfirmedDeliverySchedule = true
        });

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.UnresolvedDeliverySchedule, result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WhenPartialQuantityDelivered_ReturnsConflict()
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
            Status = OrderItemStatus.PARTIALLY_DELIVERED,
            Quantity = 5,
            DeliveredQuantity = 2
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
                Status = ProjectStatus.DELIVERING
            },
            OrderItems = [item]
        });

        var result = await service.ConfirmDeliveryAsync(orderId, _customerId);

        Assert.Equal(409, result.Status);
        Assert.Equal(OrderErrorCodes.DeliverableItemsNotDelivered, result.ErrorCode);
    }

    private static (ProjectScheduleDetailReadModel Detail, ProjectSchedule Entity) CreateConfirmedDeliverySchedule(
        Guid projectId,
        Guid scheduleId,
        Guid assignedStaffId)
    {
        var now = DateTime.UtcNow;
        return (
            new ProjectScheduleDetailReadModel
            {
                ScheduleId = scheduleId,
                ProjectId = projectId,
                ProjectName = "Test Project",
                CustomerId = Guid.NewGuid(),
                ScheduleType = ProjectScheduleType.DELIVERY,
                Status = ProjectScheduleStatus.CONFIRMED,
                AssignedStaffId = assignedStaffId,
                ScheduledStart = now.AddMinutes(-1),
                ScheduledEnd = now.AddHours(2)
            },
            new ProjectSchedule
            {
                ScheduleId = scheduleId,
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Status = ProjectScheduleStatus.CONFIRMED,
                AssignedStaffId = assignedStaffId,
                ScheduledStart = now.AddMinutes(-1),
                ScheduledEnd = now.AddHours(2),
                Title = "Delivery"
            });
    }

    private static OrderService BuildService(OrderServiceTestOptions? options = null)
    {
        options ??= new OrderServiceTestOptions();
        var payments = new EmptyPaymentRepository
        {
            SummedPaidAmount = options.SummedPaidAmount,
            ExistingRemainingPayment = options.ExistingRemainingPayment,
            AddedPayments = options.AddedPayments,
            ThrowOnAddPayment = options.ThrowOnAddPayment
        };
        return new OrderService(
            new FakeOrderRepository(
                options.Orders,
                options.OrderDetail,
                options.Order,
                options.OrderItem,
                options.OrderItems),
            new FakeProjectRepository(options.ProjectDetail, options.Role, options.Project),
            payments,
            new OrderServiceDependencies(
                new FakeProductionRequestRepository(
                    options.IsProductionCompleted,
                    options.HasAssignedCompletedProduction),
                options.Schedules ?? new EmptyProjectScheduleRepository
                {
                    ConfirmedDeliverySchedule = options.HasConfirmedDeliverySchedule,
                    ScheduleDetail = options.ScheduleDetail,
                    ScheduleEntity = options.ScheduleEntity,
                    HasUnresolvedConfirmedDeliverySchedule = options.HasUnresolvedConfirmedDeliverySchedule,
                    UnusedFutureDeliverySchedules = options.UnusedFutureDeliverySchedules
                },
                options.Deliveries ?? new FakeDeliveryRepository(),
                options.UnitOfWork,
                new SePayOptions { Currency = "VND", PaymentCodePrefix = "FS", PaymentCodeRandomDigits = 8 },
                options.Notifications,
                null));
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

    private static OrderItem CreateDeliveredOrderItem(Guid orderId)
    {
        return new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Status = OrderItemStatus.DELIVERED,
            Quantity = 1,
            DeliveredAt = DateTime.UtcNow
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

        public bool IsProductionCompleted { get; init; } = true;

        public Payment? ExistingRemainingPayment { get; init; }

        public List<Payment> AddedPayments { get; } = [];

        public bool ThrowOnAddPayment { get; init; }

        public FakeUnitOfWork UnitOfWork { get; init; } = new();

        public INotificationDispatcher? Notifications { get; init; }

        public FakeDeliveryRepository? Deliveries { get; init; }

        public EmptyProjectScheduleRepository? Schedules { get; init; }

        public ProjectScheduleDetailReadModel? ScheduleDetail { get; init; }

        public ProjectSchedule? ScheduleEntity { get; init; }

        public bool HasUnresolvedConfirmedDeliverySchedule { get; init; }

        public bool HasAssignedCompletedProduction { get; init; } = true;

        public IReadOnlyList<ProjectSchedule> UnusedFutureDeliverySchedules { get; init; } = [];

        public Guid ProductionUserId { get; init; }
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
            return Task.FromResult(items.Count > 0 &&
                items.All(item => item.Status == OrderItemStatus.READY && item.DeliveredQuantity == 0));
        }

        public Task<bool> AllDeliverableItemsDeliveredAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            var items = GetDeliverableItems(orderId);
            return Task.FromResult(items.Count > 0 &&
                items.All(item =>
                    item.Status == OrderItemStatus.DELIVERED ||
                    item.DeliveredQuantity >= (item.Quantity ?? 0)));
        }

        public Task<Order?> GetLatestByProjectInStatusesAsync(
            Guid projectId,
            IReadOnlyCollection<OrderStatus> statuses,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                order?.ProjectId == projectId &&
                order.Status.HasValue &&
                statuses.Contains(order.Status.Value)
                    ? order
                    : null);
        }

        public Task<int> GetTotalRemainingDeliverableQuantityAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            var remaining = GetDeliverableItems(orderId)
                .Sum(item => Math.Max(0, (item.Quantity ?? 0) - item.DeliveredQuantity));
            return Task.FromResult(remaining);
        }

        public Task<IReadOnlyList<OrderItem>> GetItemsByIdsForUpdateAsync(
            IReadOnlyCollection<Guid> orderItemIds,
            CancellationToken cancellationToken = default)
        {
            var items = (orderItems ?? [])
                .Where(item => orderItemIds.Contains(item.OrderItemId))
                .ToList();
            return Task.FromResult<IReadOnlyList<OrderItem>>(items);
        }

        private List<OrderItem> GetDeliverableItems(Guid orderId)
        {
            return (orderItems ?? [])
                .Where(item =>
                    item.OrderId == orderId &&
                    item.ProductVersionId.HasValue &&
                    (item.Quantity ?? 0) > 0 &&
                    item.Status is not (OrderItemStatus.UNAVAILABLE or OrderItemStatus.CANCELLED))
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

        public Payment? ExistingRemainingPayment { get; init; }

        public List<Payment> AddedPayments { get; init; } = [];

        public bool ThrowOnAddPayment { get; init; }

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
        {
            if (ThrowOnAddPayment)
            {
                throw new InvalidOperationException("Payment creation failed.");
            }

            AddedPayments.Add(payment);
            return Task.CompletedTask;
        }

        public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Payment?> GetByOrderAndTypeAsync(
            Guid orderId,
            PaymentType paymentType,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                ExistingRemainingPayment?.OrderId == orderId &&
                ExistingRemainingPayment.PaymentType == paymentType
                    ? ExistingRemainingPayment
                    : null);

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

    private sealed class FakeProductionRequestRepository(
        bool isOrderProductionCompleted,
        bool hasAssignedCompletedProduction = true) : IProductionRequestRepository
    {
        public Task<bool> IsOrderProductionCompletedAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(isOrderProductionCompleted);

        public Task<bool> HasAssignedCompletedProductionForProjectAsync(
            Guid projectId,
            Guid productionAccountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(hasAssignedCompletedProduction);

        public Task<bool> HasViewableAssignedRequestAsync(
            Guid projectId,
            Guid productionAccountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(hasAssignedCompletedProduction);

        public Task<bool> HasActiveRequestForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> CountCreatedOnAsync(DateOnly date, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<List<OrderItem>> GetProductOrderItemsAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<OrderItem>());

        public Task AddItemsAsync(List<ProductionItem> items, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> IsActiveProductionStaffAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<Infrastructure.ReadModels.Production.ProductionAssigneeReadModel?> GetAssigneeAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Production.ProductionAssigneeReadModel?>(null);

        public Task<List<Infrastructure.ReadModels.Production.AvailableProductionStaffReadModel>> GetAvailableStaffAsync(
            string? search,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Infrastructure.ReadModels.Production.AvailableProductionStaffReadModel>());

        public Task<List<Infrastructure.ReadModels.Production.ProductionRequestListItemReadModel>> GetQueueAsync(
            Infrastructure.ReadModels.Production.ProductionRequestQueueReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Infrastructure.ReadModels.Production.ProductionRequestListItemReadModel>());

        public Task<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?> GetDetailAsync(
            Guid productionRequestId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?>(null);

        public Task<ProductionItem?> GetItemByIdAsync(
            Guid productionItemId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductionItem?>(null);

        public Task<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?> GetDetailByItemIdAsync(
            Guid productionItemId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Infrastructure.ReadModels.Production.ProductionRequestDetailReadModel?>(null);

        public void UpdateItem(ProductionItem item)
        {
        }

        public IQueryable<ProductionRequest> Query() => Enumerable.Empty<ProductionRequest>().AsQueryable();

        public Task<ProductionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductionRequest?>(null);

        public Task<IReadOnlyList<ProductionRequest>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductionRequest>>([]);

        public Task AddAsync(ProductionRequest entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<ProductionRequest> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(ProductionRequest entity)
        {
        }

        public void Remove(ProductionRequest entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeDeliveryRepository : IDeliveryRepository
    {
        private readonly Dictionary<Guid, Delivery> _deliveries = new();
        private readonly Dictionary<Guid, List<DeliveryItem>> _items = new();
        private Func<Guid, DeliveryDetailReadModel>? _createDetailFactory;

        public OrderDeliveryTrackingReadModel? Tracking { get; init; }

        public List<Delivery> AddedDeliveries => _deliveries.Values.ToList();

        public Delivery? GetDelivery(Guid deliveryId) => _deliveries.GetValueOrDefault(deliveryId);

        public void SetCreateDetailFactory(Func<Guid, DeliveryDetailReadModel> factory)
        {
            _createDetailFactory = factory;
        }

        public void SeedListItem(
            Guid deliveryId,
            Guid orderId,
            DeliveryStatus status,
            int itemCount)
        {
            _deliveries[deliveryId] = new Delivery
            {
                DeliveryId = deliveryId,
                OrderId = orderId,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            _items[deliveryId] = Enumerable.Range(0, itemCount)
                .Select(_ => new DeliveryItem
                {
                    DeliveryItemId = Guid.NewGuid(),
                    DeliveryId = deliveryId,
                    OrderItemId = Guid.NewGuid(),
                    Quantity = 1
                })
                .ToList();
        }

        public void SeedDelivery(
            Guid deliveryId,
            Guid orderId,
            DeliveryStatus status,
            IReadOnlyList<DeliveryItem> items)
        {
            _deliveries[deliveryId] = new Delivery
            {
                DeliveryId = deliveryId,
                OrderId = orderId,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            _items[deliveryId] = items.ToList();
        }

        public Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default)
        {
            _deliveries[delivery.DeliveryId] = delivery;
            _items[delivery.DeliveryId] = [];
            return Task.CompletedTask;
        }

        public Task AddItemAsync(DeliveryItem item, CancellationToken cancellationToken = default)
        {
            if (!_items.TryGetValue(item.DeliveryId, out var items))
            {
                items = [];
                _items[item.DeliveryId] = items;
            }

            items.Add(item);
            return Task.CompletedTask;
        }

        public Task<DeliveryDetailReadModel?> GetDetailAsync(
            Guid orderId,
            Guid deliveryId,
            CancellationToken cancellationToken = default)
        {
            if (!_deliveries.TryGetValue(deliveryId, out var delivery) || delivery.OrderId != orderId)
            {
                return Task.FromResult<DeliveryDetailReadModel?>(null);
            }

            var items = _items.GetValueOrDefault(deliveryId) ?? [];
            if (_createDetailFactory is not null)
            {
                var created = _createDetailFactory(deliveryId);
                var mappedItems = items.Select(item => new DeliveryItemReadModel
                {
                    DeliveryItemId = item.DeliveryItemId,
                    DeliveryId = item.DeliveryId,
                    OrderItemId = item.OrderItemId,
                    Quantity = item.Quantity,
                    Note = item.Note
                }).ToList();

                return Task.FromResult<DeliveryDetailReadModel?>(new DeliveryDetailReadModel
                {
                    DeliveryId = created.DeliveryId,
                    OrderId = created.OrderId,
                    Status = created.Status,
                    CreatedBy = created.CreatedBy,
                    CompletedBy = created.CompletedBy,
                    Note = created.Note,
                    CreatedAt = created.CreatedAt,
                    CompletedAt = created.CompletedAt,
                    ItemCount = mappedItems.Count,
                    Items = mappedItems
                });
            }

            return Task.FromResult<DeliveryDetailReadModel?>(new DeliveryDetailReadModel
            {
                DeliveryId = delivery.DeliveryId,
                OrderId = delivery.OrderId,
                Status = delivery.Status,
                CreatedBy = delivery.CreatedBy,
                CompletedBy = delivery.CompletedBy,
                Note = delivery.Note,
                CreatedAt = delivery.CreatedAt,
                CompletedAt = delivery.CompletedAt,
                ItemCount = items.Count,
                Items = items.Select(item => new DeliveryItemReadModel
                {
                    DeliveryItemId = item.DeliveryItemId,
                    DeliveryId = item.DeliveryId,
                    OrderItemId = item.OrderItemId,
                    Quantity = item.Quantity,
                    Note = item.Note
                }).ToList()
            });
        }

        public Task<IReadOnlyList<DeliveryListItemReadModel>> GetByOrderAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            var result = _deliveries.Values
                .Where(delivery => delivery.OrderId == orderId)
                .Select(delivery => new DeliveryListItemReadModel
                {
                    DeliveryId = delivery.DeliveryId,
                    OrderId = delivery.OrderId,
                    Status = delivery.Status,
                    CreatedAt = delivery.CreatedAt,
                    CompletedAt = delivery.CompletedAt,
                    ItemCount = _items.GetValueOrDefault(delivery.DeliveryId)?.Count ?? 0
                })
                .ToList();
            return Task.FromResult<IReadOnlyList<DeliveryListItemReadModel>>(result);
        }

        public Task<Delivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_deliveries.GetValueOrDefault(deliveryId));

        public Task<IReadOnlyList<DeliveryItem>> GetItemsByDeliveryAsync(
            Guid deliveryId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DeliveryItem>>(_items.GetValueOrDefault(deliveryId) ?? []);

        public Task<OrderDeliveryTrackingReadModel?> GetTrackingByOrderAsync(
            Guid orderId,
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Tracking);

        public Task<bool> ExistsByProjectScheduleIdAsync(
            Guid projectScheduleId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_deliveries.Values.Any(delivery => delivery.ProjectScheduleId == projectScheduleId));

        public Task<bool> HasInProgressDeliveryAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_deliveries.Values.Any(delivery =>
                delivery.OrderId == orderId && delivery.Status == DeliveryStatus.IN_PROGRESS));

        public void Update(Delivery delivery)
        {
            _deliveries[delivery.DeliveryId] = delivery;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool BeganTransaction { get; private set; }

        public bool CommittedTransaction { get; private set; }

        public bool RolledBackTransaction { get; private set; }

        public int SaveChangesCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BeganTransaction = true;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            CommittedTransaction = true;
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            RolledBackTransaction = true;
            return Task.CompletedTask;
        }
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
