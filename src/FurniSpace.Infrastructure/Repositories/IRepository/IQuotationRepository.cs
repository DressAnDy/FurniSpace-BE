using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Quotations;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IQuotationRepository : IGenericRepository<Quotation>
{
    Task<Quotation?> GetLatestByProjectAndProposalInStatusesAsync(
        Guid projectId,
        Guid proposalId,
        IReadOnlyCollection<QuotationStatus> statuses,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuotationReadModel>> GetByProjectAsync(
        QuotationQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<QuotationDetailReadModel?> GetDetailAsync(
        Guid quotationId,
        CancellationToken cancellationToken = default);

    Task<SelectedProposalForQuotationReadModel?> GetSelectedProposalAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> HasQuotationForProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProposalItem>> GetProposalItemsAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuotationItem>> GetItemsByQuotationAsync(
        Guid quotationId,
        CancellationToken cancellationToken = default);

    Task<QuotationItem?> GetItemAsync(
        Guid quotationItemId,
        CancellationToken cancellationToken = default);

    Task AddItemAsync(
        QuotationItem item,
        CancellationToken cancellationToken = default);

    void UpdateItem(QuotationItem item);

    void RemoveItem(QuotationItem item);

    Task AddOrderAsync(
        Order order,
        CancellationToken cancellationToken = default);

    Task AddOrderItemAsync(
        OrderItem item,
        CancellationToken cancellationToken = default);
}
