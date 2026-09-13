#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Orders;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectServiceCompleteTests
{
    [Fact]
    public async Task CompleteAsync_WhenAlreadyCompleted_ReturnsSuccessIdempotently()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow.AddDays(-1);
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectName = "Completed project",
            Status = ProjectStatus.COMPLETED,
            CompletedAt = completedAt
        };
        var repository = new ProjectServiceTests.FakeProjectRepository("SALES", [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.CompleteAsync(projectId, salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectStatus.COMPLETED.ToString(), result.Data?.ProjectStatus);
        Assert.Equal(ProjectStatus.COMPLETED, project.Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenAlreadyCompletedWithoutCompletedAt_UsesUpdatedAt()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var updatedAt = DateTime.UtcNow.AddDays(-2);
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectName = "Completed project",
            Status = ProjectStatus.COMPLETED,
            CompletedAt = null,
            UpdatedAt = updatedAt
        };
        var repository = new ProjectServiceTests.FakeProjectRepository("SALES", [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.CompleteAsync(projectId, salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(updatedAt, result.Data?.CompletedAt);
    }

    [Fact]
    public async Task CompleteAsync_WithAdmin_CompletesProject()
    {
        var adminId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var project = CreateDeliveredProject(projectId, salesId);
        var orders = CreateOrderRepository(
            projectId,
            orderId,
            new Order
            {
                OrderId = orderId,
                ProjectId = projectId,
                Status = OrderStatus.COMPLETED,
                CustomerConfirmedDeliveryAt = DateTime.UtcNow
            },
            OrderStatus.COMPLETED,
            CreateDeliveredItem(orderId));
        var repository = new ProjectServiceTests.FakeProjectRepository("ADMIN", [project]);
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync),
            new ProjectServiceFactoryOptions { Orders = orders });

        var result = await service.CompleteAsync(projectId, adminId);

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectStatus.COMPLETED, project.Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenEmptyProjectId_ReturnsBadRequest()
    {
        var service = ProjectServiceTestFactory.Create(
            new ProjectServiceTests.FakeProjectRepository("SALES"),
            TestUnitOfWork.Instance);

        var result = await service.CompleteAsync(Guid.Empty, Guid.NewGuid());

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenUnauthorized_ReturnsUnauthorized()
    {
        var service = ProjectServiceTestFactory.Create(
            new ProjectServiceTests.FakeProjectRepository("SALES"),
            TestUnitOfWork.Instance);

        var result = await service.CompleteAsync(Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenProjectNotFound_ReturnsNotFound()
    {
        var service = ProjectServiceTestFactory.Create(
            new ProjectServiceTests.FakeProjectRepository("SALES"),
            TestUnitOfWork.Instance);

        var result = await service.CompleteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenForbidden_ReturnsForbidden()
    {
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectName = "Delivered project",
            Status = ProjectStatus.DELIVERED
        };
        var service = ProjectServiceTestFactory.Create(
            new ProjectServiceTests.FakeProjectRepository("SALES", [project]),
            TestUnitOfWork.Instance);

        var result = await service.CompleteAsync(project.ProjectId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenProjectNotDelivered_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectName = "In production project",
            Status = ProjectStatus.IN_PRODUCTION
        };
        var service = ProjectServiceTestFactory.Create(
            new ProjectServiceTests.FakeProjectRepository("SALES", [project]),
            TestUnitOfWork.Instance);

        var result = await service.CompleteAsync(projectId, salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectErrorCodes.ProjectNotDelivered, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenRelatedOrderMissing_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateDeliveredProject(projectId, salesId);
        var service = ProjectServiceTestFactory.Create(
            new ProjectServiceTests.FakeProjectRepository("SALES", [project]),
            TestUnitOfWork.Instance,
            new ProjectServiceFactoryOptions { Orders = new FakeProjectOrderRepository() });

        var result = await service.CompleteAsync(projectId, salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectErrorCodes.RelatedOrderNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenRelatedOrderNotCompleted_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var project = CreateDeliveredProject(projectId, salesId);
        var orders = CreateOrderRepository(
            projectId,
            orderId,
            new Order
            {
                OrderId = orderId,
                ProjectId = projectId,
                Status = OrderStatus.FINAL_PAYMENT_PENDING,
                CustomerConfirmedDeliveryAt = DateTime.UtcNow
            },
            OrderStatus.FINAL_PAYMENT_PENDING,
            CreateDeliveredItem(orderId));
        var service = ProjectServiceTestFactory.Create(
            new ProjectServiceTests.FakeProjectRepository("SALES", [project]),
            TestUnitOfWork.Instance,
            new ProjectServiceFactoryOptions { Orders = orders });

        var result = await service.CompleteAsync(projectId, salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectErrorCodes.RelatedOrderNotCompleted, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenDeliveryNotConfirmed_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var project = CreateDeliveredProject(projectId, salesId);
        var orders = CreateOrderRepository(
            projectId,
            orderId,
            new Order
            {
                OrderId = orderId,
                ProjectId = projectId,
                Status = OrderStatus.COMPLETED,
                CustomerConfirmedDeliveryAt = null
            },
            OrderStatus.COMPLETED,
            CreateDeliveredItem(orderId));
        var service = ProjectServiceTestFactory.Create(
            new ProjectServiceTests.FakeProjectRepository("SALES", [project]),
            TestUnitOfWork.Instance,
            new ProjectServiceFactoryOptions { Orders = orders });

        var result = await service.CompleteAsync(projectId, salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectErrorCodes.DeliveryNotConfirmed, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenItemsNotDelivered_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var project = CreateDeliveredProject(projectId, salesId);
        var orders = CreateOrderRepository(
            projectId,
            orderId,
            new Order
            {
                OrderId = orderId,
                ProjectId = projectId,
                Status = OrderStatus.COMPLETED,
                CustomerConfirmedDeliveryAt = DateTime.UtcNow
            },
            OrderStatus.COMPLETED,
            new OrderItem
            {
                OrderId = orderId,
                ProductVersionId = Guid.NewGuid(),
                Quantity = 1,
                Status = OrderItemStatus.READY
            });
        var service = ProjectServiceTestFactory.Create(
            new ProjectServiceTests.FakeProjectRepository("SALES", [project]),
            TestUnitOfWork.Instance,
            new ProjectServiceFactoryOptions { Orders = orders });

        var result = await service.CompleteAsync(projectId, salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectErrorCodes.DeliveryNotConfirmed, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenOrderEntityMissing_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var project = CreateDeliveredProject(projectId, salesId);
        var orders = new FakeProjectOrderRepository
        {
            ProjectOrders =
            [
                new OrderListItemReadModel
                {
                    OrderId = orderId,
                    ProjectId = projectId,
                    Status = OrderStatus.COMPLETED
                }
            ]
        };
        var service = ProjectServiceTestFactory.Create(
            new ProjectServiceTests.FakeProjectRepository("SALES", [project]),
            TestUnitOfWork.Instance,
            new ProjectServiceFactoryOptions { Orders = orders });

        var result = await service.CompleteAsync(projectId, salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectErrorCodes.RelatedOrderNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenOrderCompleted_CompletesProjectAndNotifies()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var project = CreateDeliveredProject(projectId, salesId);
        var orders = CreateOrderRepository(
            projectId,
            orderId,
            new Order
            {
                OrderId = orderId,
                ProjectId = projectId,
                Status = OrderStatus.COMPLETED,
                CustomerConfirmedDeliveryAt = DateTime.UtcNow
            },
            OrderStatus.COMPLETED,
            CreateDeliveredItem(orderId));
        var dispatcher = new FakeNotificationDispatcher();
        var repository = new ProjectServiceTests.FakeProjectRepository("SALES", [project]);
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync),
            new ProjectServiceFactoryOptions { Dispatcher = dispatcher, Orders = orders });

        var result = await service.CompleteAsync(projectId, salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectStatus.COMPLETED, project.Status);
        Assert.NotNull(project.CompletedAt);
        Assert.Equal(NotificationType.ProjectStatusChanged, dispatcher.LastType);
    }

    private static Project CreateDeliveredProject(Guid projectId, Guid salesId)
    {
        return new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectName = "Delivered project",
            Status = ProjectStatus.DELIVERED
        };
    }

    private static FakeProjectOrderRepository CreateOrderRepository(
        Guid projectId,
        Guid orderId,
        Order orderEntity,
        OrderStatus listStatus,
        OrderItem item)
    {
        return new FakeProjectOrderRepository
        {
            Order = orderEntity,
            ProjectOrders =
            [
                new OrderListItemReadModel
                {
                    OrderId = orderId,
                    ProjectId = projectId,
                    Status = listStatus
                }
            ],
            OrderItems = [item]
        };
    }

    private static OrderItem CreateDeliveredItem(Guid orderId)
    {
        return new OrderItem
        {
            OrderId = orderId,
            ProductVersionId = Guid.NewGuid(),
            Quantity = 1,
            Status = OrderItemStatus.DELIVERED
        };
    }

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        public NotificationType? LastType { get; private set; }

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            NotificationDispatchRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            LastType = type;
            return Task.CompletedTask;
        }
    }
}
