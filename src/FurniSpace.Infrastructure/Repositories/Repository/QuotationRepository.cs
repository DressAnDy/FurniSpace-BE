using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Quotations;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class QuotationRepository : GenericRepository<Quotation>, IQuotationRepository
{
    public QuotationRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<Quotation?> GetLatestByProjectAndProposalInStatusesAsync(
        Guid projectId,
        Guid proposalId,
        IReadOnlyCollection<QuotationStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        return DbContext.QuotationSet
            .Where(quotation =>
                quotation.ProjectId == projectId &&
                quotation.ProposalId == proposalId &&
                quotation.Status.HasValue &&
                statuses.Contains(quotation.Status.Value))
            .OrderByDescending(quotation => quotation.VersionNo)
            .ThenByDescending(quotation => quotation.CreatedAt)
            .ThenByDescending(quotation => quotation.QuotationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuotationReadModel>> GetByProjectAsync(
        QuotationQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFilters(BuildQuotationQuery(), query)
            .OrderByDescending(quotation => quotation.VersionNo)
            .ThenByDescending(quotation => quotation.CreatedAt)
            .ThenByDescending(quotation => quotation.QuotationId)
            .ToListAsync(cancellationToken);
    }

    public async Task<QuotationDetailReadModel?> GetDetailAsync(
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        var quotation = await BuildQuotationQuery()
            .Where(item => item.QuotationId == quotationId)
            .Select(item => new QuotationDetailReadModel
            {
                QuotationId = item.QuotationId,
                ProjectId = item.ProjectId,
                ProposalId = item.ProposalId,
                QuotationCode = item.QuotationCode,
                VersionNo = item.VersionNo,
                SubtotalAmount = item.SubtotalAmount,
                TotalDiscountAmount = item.TotalDiscountAmount,
                PreVatAmount = item.PreVatAmount,
                VatRate = item.VatRate,
                VatAmount = item.VatAmount,
                TotalAmount = item.TotalAmount,
                DepositAmount = item.DepositAmount,
                Currency = item.Currency,
                Status = item.Status,
                ValidUntil = item.ValidUntil,
                CustomerNote = item.CustomerNote,
                SalesNote = item.SalesNote,
                SentAt = item.SentAt,
                AcceptedAt = item.AcceptedAt,
                RejectedAt = item.RejectedAt,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                CustomerId = item.CustomerId,
                AssignedSalesId = item.AssignedSalesId,
                AssignedDesignerId = item.AssignedDesignerId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (quotation is null)
        {
            return null;
        }

        quotation.Items = await GetItemsAsync(quotationId, cancellationToken);
        return quotation;
    }

    public Task<SelectedProposalForQuotationReadModel?> GetSelectedProposalAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectSet
            .Where(project => project.ProjectId == projectId)
            .Join(
                DbContext.ProposalSet.Where(proposal => proposal.Status == ProposalStatus.SELECTED),
                project => project.ProjectId,
                proposal => proposal.ProjectId,
                (project, proposal) => new SelectedProposalForQuotationReadModel
                {
                    ProjectId = project.ProjectId,
                    ProposalId = proposal.ProposalId,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId,
                    ProjectStatus = project.Status,
                    ProposalStatus = proposal.Status
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> HasQuotationForProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.QuotationSet.AnyAsync(
            quotation =>
                quotation.ProposalId == proposalId &&
                quotation.Status != QuotationStatus.CANCELLED,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProposalItem>> GetProposalItemsAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.ProposalItemSet
            .Where(item => item.ProposalId == proposalId)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.ProposalItemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuotationItem>> GetItemsByQuotationAsync(
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.QuotationItemSet
            .Where(item => item.QuotationId == quotationId)
            .ToListAsync(cancellationToken);
    }

    public Task<QuotationItem?> GetItemAsync(
        Guid quotationItemId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.QuotationItemSet
            .FirstOrDefaultAsync(item => item.QuotationItemId == quotationItemId, cancellationToken);
    }

    public Task AddItemAsync(
        QuotationItem item,
        CancellationToken cancellationToken = default)
    {
        return DbContext.QuotationItemSet.AddAsync(item, cancellationToken).AsTask();
    }

    public void UpdateItem(QuotationItem item)
    {
        DbContext.QuotationItemSet.Update(item);
    }

    public void RemoveItem(QuotationItem item)
    {
        DbContext.QuotationItemSet.Remove(item);
    }

    public Task AddOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OrderSet.AddAsync(order, cancellationToken).AsTask();
    }

    public Task AddOrderItemAsync(
        OrderItem item,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OrderItemSet.AddAsync(item, cancellationToken).AsTask();
    }

    private IQueryable<QuotationReadModel> BuildQuotationQuery()
    {
        return DbContext.QuotationSet
            .Join(
                DbContext.ProjectSet,
                quotation => quotation.ProjectId,
                project => project.ProjectId,
                (quotation, project) => new QuotationReadModel
                {
                    QuotationId = quotation.QuotationId,
                    ProjectId = quotation.ProjectId,
                    ProposalId = quotation.ProposalId,
                    QuotationCode = quotation.QuotationCode,
                    VersionNo = quotation.VersionNo,
                    SubtotalAmount = quotation.SubtotalAmount,
                    TotalDiscountAmount = quotation.TotalDiscountAmount,
                    PreVatAmount = quotation.PreVatAmount,
                    VatRate = quotation.VatRate,
                    VatAmount = quotation.VatAmount,
                    TotalAmount = quotation.TotalAmount,
                    DepositAmount = quotation.DepositAmount,
                    Currency = quotation.Currency,
                    Status = quotation.Status,
                    ValidUntil = quotation.ValidUntil,
                    CustomerNote = quotation.CustomerNote,
                    SalesNote = quotation.SalesNote,
                    SentAt = quotation.SentAt,
                    AcceptedAt = quotation.AcceptedAt,
                    RejectedAt = quotation.RejectedAt,
                    CreatedAt = quotation.CreatedAt,
                    UpdatedAt = quotation.UpdatedAt,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId
                });
    }

    private async Task<IReadOnlyList<QuotationItemReadModel>> GetItemsAsync(
        Guid quotationId,
        CancellationToken cancellationToken)
    {
        return await DbContext.QuotationItemSet
            .Where(item => item.QuotationId == quotationId)
            .OrderBy(item => item.DisplayOrder ?? int.MaxValue)
            .ThenBy(item => item.QuotationItemId)
            .Select(item => new QuotationItemReadModel
            {
                QuotationItemId = item.QuotationItemId,
                QuotationId = item.QuotationId,
                ProposalItemId = item.ProposalItemId,
                ProductVersionId = item.ProductVersionId,
                ProductNameSnapshot = item.ProductNameSnapshot,
                ProductVersionNameSnapshot = item.ProductVersionNameSnapshot,
                ProductVersionCodeSnapshot = item.ProductVersionCodeSnapshot,
                ItemName = item.ItemName,
                Description = item.Description,
                DisplayOrder = item.DisplayOrder,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                GrossAmount = item.GrossAmount,
                DiscountAmount = item.DiscountAmount,
                TotalAmount = item.TotalAmount,
                IsCustomized = item.IsCustomized,
                CustomizationNote = item.CustomizationNote,
                Note = item.Note,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<QuotationReadModel> ApplyFilters(
        IQueryable<QuotationReadModel> query,
        QuotationQueryReadModel filter)
    {
        query = query.Where(quotation => quotation.ProjectId == filter.ProjectId);
        return filter.Status.HasValue
            ? query.Where(quotation => quotation.Status == filter.Status.Value)
            : query;
    }
}
