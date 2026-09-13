#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.Production;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class ProductionRequestRepositoryInterfaceDefaultsTests
{
    [Fact]
    public async Task DefaultInterfaceMethods_ReturnConfiguredFallbacks()
    {
        IProductionRequestRepository repository = new MinimalProductionRequestRepository();
        var orderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        Assert.False(await repository.ExistsForOrderAsync(orderId));
        Assert.False(await repository.IsOrderProductionCompletedAsync(orderId));
        Assert.False(await repository.HasAssignedCompletedProductionForProjectAsync(projectId, staffId));
        Assert.Empty(await repository.GetItemsByRequestIdAsync(requestId));
        Assert.Null(await repository.GetMaxOperationalProductionDateAsync(projectId));
    }

    private sealed class MinimalProductionRequestRepository : IProductionRequestRepository
    {
        public Task<bool> HasActiveRequestForOrderAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> CountCreatedOnAsync(DateOnly date, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<OrderItem>> GetProductOrderItemsAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddItemsAsync(List<ProductionItem> items, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> IsActiveProductionStaffAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductionAssigneeReadModel?> GetAssigneeAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<AvailableProductionStaffReadModel>> GetAvailableStaffAsync(
            string? search,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<ProductionRequestListItemReadModel>> GetQueueAsync(
            ProductionRequestQueueReadModel query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> HasViewableAssignedRequestAsync(
            Guid projectId,
            Guid productionAccountId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductionRequestDetailReadModel?> GetDetailAsync(
            Guid productionRequestId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductionItem?> GetItemByIdAsync(
            Guid productionItemId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductionRequestDetailReadModel?> GetDetailByItemIdAsync(
            Guid productionItemId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void UpdateItem(ProductionItem item) => throw new NotSupportedException();

        public IQueryable<ProductionRequest> Query() => throw new NotSupportedException();

        public Task<ProductionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ProductionRequest>> ListAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddAsync(ProductionRequest entity, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddRangeAsync(IEnumerable<ProductionRequest> entities, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Update(ProductionRequest entity) => throw new NotSupportedException();

        public void Remove(ProductionRequest entity) => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
