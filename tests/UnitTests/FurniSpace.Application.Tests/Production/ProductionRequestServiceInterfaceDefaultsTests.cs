#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Production;
using Xunit;

namespace FurniSpace.Application.Tests.Production;

public sealed class ProductionRequestServiceInterfaceDefaultsTests
{
    [Fact]
    public async Task CompleteAsync_DefaultImplementation_ReturnsUnauthorized()
    {
        IProductionRequestService service = new MinimalProductionRequestService();

        var result = await service.CompleteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(401, result.Status);
    }

    private sealed class MinimalProductionRequestService : IProductionRequestService
    {
        public Task<ServiceResult<ProductionRequestCreatedDto>> CreateAsync(
            Guid orderId,
            Guid currentUserId,
            CreateProductionRequestDto request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<List<AvailableProductionStaffDto>>> GetAvailableStaffAsync(
            Guid currentUserId,
            AvailableProductionStaffQueryDto query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductionRequestAssignmentDto>> AssignAsync(
            Guid productionRequestId,
            Guid currentUserId,
            AssignProductionRequestDto request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductionRequestListResponseDto>> GetQueueAsync(
            Guid currentUserId,
            ProductionRequestQueryDto query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductionRequestDetailDto>> GetDetailAsync(
            Guid productionRequestId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductionRequestStatusDto>> StartAsync(
            Guid productionRequestId,
            Guid currentUserId,
            StartProductionRequestDto request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<ProductionItemStatusDto>> UpdateItemStatusAsync(
            Guid productionItemId,
            Guid currentUserId,
            UpdateProductionItemStatusDto request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
