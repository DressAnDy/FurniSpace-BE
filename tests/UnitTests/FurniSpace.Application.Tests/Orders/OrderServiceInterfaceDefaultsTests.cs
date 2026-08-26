#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.Interfaces.Orders;
using Xunit;

namespace FurniSpace.Application.Tests.Orders;

public sealed class OrderServiceInterfaceDefaultsTests
{
    [Fact]
    public async Task DefaultInterfaceMethods_ReturnUnauthorized()
    {
        IOrderService service = new MinimalOrderService();

        Assert.Equal(401, (await service.StartDeliveryAsync(Guid.NewGuid(), Guid.NewGuid())).Status);
        Assert.Equal(401, (await service.CompleteDeliveryAsync(Guid.NewGuid(), Guid.NewGuid())).Status);
        Assert.Equal(401, (await service.ConfirmDeliveryAsync(Guid.NewGuid(), Guid.NewGuid())).Status);
        Assert.Equal(401, (await service.PrepareFinalPaymentAsync(Guid.NewGuid(), Guid.NewGuid())).Status);
        Assert.Equal(401, (await service.CompleteAsync(Guid.NewGuid(), Guid.NewGuid())).Status);
        Assert.Equal(401, (await service.CreateDeliveryBatchAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateDeliveryBatchRequestDto())).Status);
        Assert.Equal(401, (await service.GetDeliveriesAsync(Guid.NewGuid(), Guid.NewGuid())).Status);
        Assert.Equal(401, (await service.GetDeliveryDetailAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())).Status);
        Assert.Equal(401, (await service.CompleteDeliveryBatchAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())).Status);
        Assert.Equal(401, (await service.GetDeliveryTrackingAsync(Guid.NewGuid(), Guid.NewGuid())).Status);
    }

    private sealed class MinimalOrderService : IOrderService
    {
        public Task<ServiceResult<OrderListResponseDto>> GetByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<OrderDetailDto>> GetDetailAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
