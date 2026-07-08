using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class CustomizationRequestRepository
    : GenericRepository<CustomizationRequest>, ICustomizationRequestRepository
{
    private static readonly CustomizationStatus[] PendingFinalSelectionStatuses =
    [
        CustomizationStatus.SUBMITTED,
        CustomizationStatus.DESIGN_REVIEWING,
        CustomizationStatus.PRODUCTION_REVIEWING,
        CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL
    ];

    public CustomizationRequestRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<CustomizationRequestReadModel>> GetByProjectAsync(
        CustomizationRequestQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFilters(BuildListQuery(), query)
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.CustomizationRequestId)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomizationRequestDetailReadModel?> GetDetailAsync(
        Guid customizationRequestId,
        CancellationToken cancellationToken = default)
    {
        var readModel = await BuildListQuery()
            .Where(request => request.CustomizationRequestId == customizationRequestId)
            .FirstOrDefaultAsync(cancellationToken);
        if (readModel is null)
        {
            return null;
        }

        var proposalItem = await DbContext.ProposalItemSet
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.ProposalItemId == readModel.ProposalItemId,
                cancellationToken);
        if (proposalItem is null)
        {
            return null;
        }

        return CustomizationRequestRepositoryProjections.ToDetailReadModel(readModel, proposalItem);
    }

    public Task<CustomizationSubmitContextReadModel?> GetSubmitContextAsync(
        Guid proposalItemId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProposalItemSet
            .Where(item => item.ProposalItemId == proposalItemId)
            .Join(
                DbContext.ProposalSet,
                item => item.ProposalId,
                proposal => proposal.ProposalId,
                (item, proposal) => new { item, proposal })
            .Join(
                DbContext.ProjectSet,
                joined => joined.proposal.ProjectId,
                project => project.ProjectId,
                (joined, project) => new CustomizationSubmitContextReadModel
                {
                    ProposalItemId = joined.item.ProposalItemId,
                    ProposalId = joined.proposal.ProposalId,
                    ProjectId = project.ProjectId,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId,
                    ProposalStatus = joined.proposal.Status,
                    ProjectStatus = project.Status,
                    ProjectName = project.ProjectName,
                    ProposalName = joined.proposal.ProposalName
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> HasQuotationForProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.QuotationSet.AnyAsync(
            quotation => quotation.ProposalId == proposalId,
            cancellationToken);
    }

    public Task<bool> HasProductionVisibleRequestAsync(
        Guid projectId,
        Guid productionUserId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.CustomizationRequestSet.AnyAsync(
            request =>
                request.ProjectId == projectId &&
                (request.Status == CustomizationStatus.PRODUCTION_REVIEWING ||
                request.ProductionReviewBy == productionUserId),
            cancellationToken);
    }

    public Task<bool> HasPendingForProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.CustomizationRequestSet.AnyAsync(
            request =>
                request.ProposalId == proposalId &&
                request.Status.HasValue &&
                PendingFinalSelectionStatuses.Contains(request.Status.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProductionCustomizationRequestQueueReadModel>> GetProductionQueueAsync(
        ProductionCustomizationRequestQueueQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await ApplyProductionQueueFilters(BuildProductionQueueQuery(), query)
            .OrderByDescending(request => request.UpdatedAt)
            .ThenByDescending(request => request.CustomizationRequestId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountProductionQueueAsync(
        ProductionCustomizationRequestQueueQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return ApplyProductionQueueFilters(BuildProductionQueueQuery(), query)
            .CountAsync(cancellationToken);
    }

    private IQueryable<ProductionCustomizationRequestQueueReadModel> BuildProductionQueueQuery()
    {
        return DbContext.CustomizationRequestSet
            .Join(
                DbContext.ProjectSet,
                request => request.ProjectId,
                project => project.ProjectId,
                (request, project) => new { request, project })
            .Join(
                DbContext.ProposalSet,
                joined => joined.request.ProposalId,
                proposal => proposal.ProposalId,
                (joined, proposal) => new { joined.request, joined.project, proposal })
            .Join(
                DbContext.ProposalItemSet,
                joined => joined.request.ProposalItemId,
                item => item.ProposalItemId,
                (joined, item) => new CustomizationRequestRepositoryProjections.ProductionQueueJoin
                {
                    Request = joined.request,
                    Project = joined.project,
                    Proposal = joined.proposal,
                    Item = item
                })
            .Select(CustomizationRequestRepositoryProjections.ProductionQueueReadModel);
    }

    private static IQueryable<ProductionCustomizationRequestQueueReadModel> ApplyProductionQueueFilters(
        IQueryable<ProductionCustomizationRequestQueueReadModel> query,
        ProductionCustomizationRequestQueueQueryReadModel filter)
    {
        if (filter.Statuses is { Count: > 0 })
        {
            query = query.Where(
                request => request.Status.HasValue && filter.Statuses.Contains(request.Status.Value));
        }

        if (filter.ProjectId.HasValue)
        {
            query = query.Where(request => request.ProjectId == filter.ProjectId.Value);
        }

        if (filter.ProposalId.HasValue)
        {
            query = query.Where(request => request.ProposalId == filter.ProposalId.Value);
        }

        if (filter.MaterialAvailable.HasValue)
        {
            query = query.Where(request => request.MaterialAvailable == filter.MaterialAvailable.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(
                request => request.UpdatedAt.HasValue && request.UpdatedAt.Value >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(
                request => request.UpdatedAt.HasValue && request.UpdatedAt.Value <= filter.ToDate.Value);
        }

        return query;
    }

    private IQueryable<CustomizationRequestReadModel> BuildListQuery()
    {
        return DbContext.CustomizationRequestSet
            .Join(
                DbContext.ProjectSet,
                request => request.ProjectId,
                project => project.ProjectId,
                (request, project) => new CustomizationRequestRepositoryProjections.RequestProjectJoin
                {
                    Request = request,
                    Project = project
                })
            .Select(CustomizationRequestRepositoryProjections.RequestProjectReadModel);
    }

    private static IQueryable<CustomizationRequestReadModel> ApplyFilters(
        IQueryable<CustomizationRequestReadModel> query,
        CustomizationRequestQueryReadModel filter)
    {
        query = query.Where(request => request.ProjectId == filter.ProjectId);
        if (filter.ProposalId.HasValue)
        {
            query = query.Where(request => request.ProposalId == filter.ProposalId.Value);
        }

        if (filter.ProposalItemId.HasValue)
        {
            query = query.Where(request => request.ProposalItemId == filter.ProposalItemId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(request => request.Status == filter.Status.Value);
        }

        return query;
    }
}
