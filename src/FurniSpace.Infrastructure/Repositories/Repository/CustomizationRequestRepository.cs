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

    public Task<CustomizationRequestDetailReadModel?> GetDetailAsync(
        Guid customizationRequestId,
        CancellationToken cancellationToken = default)
    {
        return BuildDetailQuery()
            .Where(request => request.CustomizationRequestId == customizationRequestId)
            .FirstOrDefaultAsync(cancellationToken);
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

    private IQueryable<CustomizationRequestReadModel> BuildListQuery()
    {
        return DbContext.CustomizationRequestSet
            .Join(
                DbContext.ProjectSet,
                request => request.ProjectId,
                project => project.ProjectId,
                (request, project) => new CustomizationRequestReadModel
                {
                    CustomizationRequestId = request.CustomizationRequestId,
                    ProjectId = request.ProjectId,
                    ProposalId = request.ProposalId,
                    ProposalItemId = request.ProposalItemId,
                    RequestedByCustomerId = request.RequestedByCustomerId,
                    RequestTitle = request.RequestTitle,
                    RequestDescription = request.RequestDescription,
                    RequestedWidth = request.RequestedWidth,
                    RequestedHeight = request.RequestedHeight,
                    RequestedDepth = request.RequestedDepth,
                    RequestedMaterial = request.RequestedMaterial,
                    RequestedColor = request.RequestedColor,
                    RequestedChangeNote = request.RequestedChangeNote,
                    DesignerId = request.DesignerId,
                    DesignerSpecNote = request.DesignerSpecNote,
                    ProductionReviewBy = request.ProductionReviewBy,
                    FeasibilityNote = request.FeasibilityNote,
                    EstimatedProductionDays = request.EstimatedProductionDays,
                    EstimatedAdditionalCost = request.EstimatedAdditionalCost,
                    AdditionalCostReason = request.AdditionalCostReason,
                    MaterialAvailable = request.MaterialAvailable,
                    ProductionRiskNote = request.ProductionRiskNote,
                    SalesReviewBy = request.SalesReviewBy,
                    ApprovedProductVersionId = request.ApprovedProductVersionId,
                    Status = request.Status,
                    CustomerAcceptedAt = request.CustomerAcceptedAt,
                    CustomerRejectedAt = request.CustomerRejectedAt,
                    CreatedAt = request.CreatedAt,
                    UpdatedAt = request.UpdatedAt,
                    CustomerId = project.CustomerId,
                    ProjectName = project.ProjectName,
                    AssignedSalesId = project.AssignedSalesId,
                    AssignedDesignerId = project.AssignedDesignerId
                });
    }

    private IQueryable<CustomizationRequestDetailReadModel> BuildDetailQuery()
    {
        return DbContext.CustomizationRequestSet
            .Join(
                DbContext.ProjectSet,
                request => request.ProjectId,
                project => project.ProjectId,
                (request, project) => new { request, project })
            .Join(
                DbContext.ProposalItemSet,
                joined => joined.request.ProposalItemId,
                item => item.ProposalItemId,
                (joined, item) => new CustomizationRequestDetailReadModel
                {
                    CustomizationRequestId = joined.request.CustomizationRequestId,
                    ProjectId = joined.request.ProjectId,
                    ProposalId = joined.request.ProposalId,
                    ProposalItemId = joined.request.ProposalItemId,
                    RequestedByCustomerId = joined.request.RequestedByCustomerId,
                    RequestTitle = joined.request.RequestTitle,
                    RequestDescription = joined.request.RequestDescription,
                    RequestedWidth = joined.request.RequestedWidth,
                    RequestedHeight = joined.request.RequestedHeight,
                    RequestedDepth = joined.request.RequestedDepth,
                    RequestedMaterial = joined.request.RequestedMaterial,
                    RequestedColor = joined.request.RequestedColor,
                    RequestedChangeNote = joined.request.RequestedChangeNote,
                    DesignerId = joined.request.DesignerId,
                    DesignerSpecNote = joined.request.DesignerSpecNote,
                    ProductionReviewBy = joined.request.ProductionReviewBy,
                    FeasibilityNote = joined.request.FeasibilityNote,
                    EstimatedProductionDays = joined.request.EstimatedProductionDays,
                    EstimatedAdditionalCost = joined.request.EstimatedAdditionalCost,
                    AdditionalCostReason = joined.request.AdditionalCostReason,
                    MaterialAvailable = joined.request.MaterialAvailable,
                    ProductionRiskNote = joined.request.ProductionRiskNote,
                    SalesReviewBy = joined.request.SalesReviewBy,
                    ApprovedProductVersionId = joined.request.ApprovedProductVersionId,
                    Status = joined.request.Status,
                    CustomerAcceptedAt = joined.request.CustomerAcceptedAt,
                    CustomerRejectedAt = joined.request.CustomerRejectedAt,
                    CreatedAt = joined.request.CreatedAt,
                    UpdatedAt = joined.request.UpdatedAt,
                    CustomerId = joined.project.CustomerId,
                    ProjectName = joined.project.ProjectName,
                    AssignedSalesId = joined.project.AssignedSalesId,
                    AssignedDesignerId = joined.project.AssignedDesignerId,
                    ProductVersionId = item.ProductVersionId,
                    ItemName = item.ItemName,
                    ItemType = item.ItemType,
                    Quantity = item.Quantity,
                    Width = item.Width,
                    Height = item.Height,
                    Depth = item.Depth,
                    Material = item.Material,
                    Color = item.Color,
                    UnitPriceSnapshot = item.UnitPriceSnapshot,
                    TotalPriceSnapshot = item.TotalPriceSnapshot,
                    ItemNote = item.Note
                });
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
