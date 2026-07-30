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

        var detail = CustomizationRequestRepositoryProjections.ToDetailReadModel(readModel);
        var sourceVersion = await DbContext.ProductVersionSet
            .AsNoTracking()
            .FirstOrDefaultAsync(
                version => version.ProductVersionId == readModel.ProductVersionId,
                cancellationToken);
        if (sourceVersion is not null)
        {
            detail.SourceProductVersion = sourceVersion;
        }

        return detail;
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
                    ProductVersionId = joined.item.ProductVersionId,
                    ProposalId = joined.proposal.ProposalId,
                    ProjectId = project.ProjectId,
                    CustomerId = project.CustomerId,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId,
                    ProposalStatus = joined.proposal.Status,
                    ProjectStatus = project.Status,
                    ProjectName = project.ProjectName,
                    ProjectCode = project.ProjectCode,
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

    public Task<bool> HasActiveRequestForProductVersionAsync(
        Guid projectId,
        Guid proposalId,
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.CustomizationRequestSet.AnyAsync(
            request =>
                request.ProjectId == projectId &&
                request.ProposalId == proposalId &&
                request.ProductVersionId == productVersionId &&
                request.Status.HasValue &&
                PendingFinalSelectionStatuses.Contains(request.Status.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProductionCustomizationRequestQueueReadModel>> GetProductionQueueAsync(
        ProductionCustomizationRequestQueueQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await ApplyProductionQueueFilters(BuildProductionQueueQuery(), query)
            .OrderByDescending(request => request.Request.UpdatedAt)
            .ThenByDescending(request => request.Request.CustomizationRequestId)
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
        return BuildListQuery()
            .Join(
                DbContext.ProposalSet,
                readModel => readModel.ProposalId,
                proposal => proposal.ProposalId,
                (readModel, proposal) => new { readModel, proposal })
            .Join(
                DbContext.ProductVersionSet,
                joined => joined.readModel.ProductVersionId,
                version => version.ProductVersionId,
                (joined, version) => new ProductionCustomizationRequestQueueReadModel
                {
                    Request = joined.readModel,
                    ProposalName = joined.proposal.ProposalName,
                    ProposalStatus = joined.proposal.Status,
                    SourceProductVersion = version
                });
    }

    private static IQueryable<ProductionCustomizationRequestQueueReadModel> ApplyProductionQueueFilters(
        IQueryable<ProductionCustomizationRequestQueueReadModel> query,
        ProductionCustomizationRequestQueueQueryReadModel filter)
    {
        if (filter.Statuses is { Count: > 0 })
        {
            query = query.Where(
                request =>
                    request.Request.Status.HasValue &&
                    filter.Statuses.Contains(request.Request.Status.Value));
        }

        if (filter.ProjectId.HasValue)
        {
            query = query.Where(request => request.Request.ProjectId == filter.ProjectId.Value);
        }

        if (filter.ProposalId.HasValue)
        {
            query = query.Where(request => request.Request.ProposalId == filter.ProposalId.Value);
        }

        if (filter.MaterialAvailable.HasValue)
        {
            query = query.Where(request => request.Request.MaterialAvailable == filter.MaterialAvailable.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(
                request =>
                    request.Request.UpdatedAt.HasValue &&
                    request.Request.UpdatedAt.Value >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(
                request =>
                    request.Request.UpdatedAt.HasValue &&
                    request.Request.UpdatedAt.Value <= filter.ToDate.Value);
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

        if (filter.ProductVersionId.HasValue)
        {
            query = query.Where(request => request.ProductVersionId == filter.ProductVersionId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(request => request.Status == filter.Status.Value);
        }

        return query;
    }
}
