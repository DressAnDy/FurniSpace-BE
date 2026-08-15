#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Interfaces.Orders;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderServiceInterfaceDefaultTests
{
    [Fact]
    public async Task StartDeliveryAsync_DefaultImplementation_ReturnsUnauthorized()
    {
        IOrderService service = new MinimalOrderService();

        var result = await service.StartDeliveryAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task CompleteDeliveryAsync_DefaultImplementation_ReturnsUnauthorized()
    {
        IOrderService service = new MinimalOrderService();

        var result = await service.CompleteDeliveryAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_DefaultImplementation_ReturnsUnauthorized()
    {
        IOrderService service = new MinimalOrderService();

        var result = await service.ConfirmDeliveryAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task PrepareFinalPaymentAsync_DefaultImplementation_ReturnsUnauthorized()
    {
        IOrderService service = new MinimalOrderService();

        var result = await service.PrepareFinalPaymentAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task CompleteAsync_DefaultImplementation_ReturnsUnauthorized()
    {
        IOrderService service = new MinimalOrderService();

        var result = await service.CompleteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(401, result.Status);
    }

    private sealed class MinimalOrderService : IOrderService
    {
        public Task<ServiceResult<OrderListResponseDto>> GetByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<OrderListResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<OrderDetailDto>> GetDetailAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<OrderDetailDto>.Unauthorized());
        }
    }
}
