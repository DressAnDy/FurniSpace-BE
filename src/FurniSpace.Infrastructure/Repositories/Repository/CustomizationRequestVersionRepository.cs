using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class CustomizationRequestVersionRepository
    : GenericRepository<CustomizationRequestVersion>, ICustomizationRequestVersionRepository
{
    public CustomizationRequestVersionRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<CustomizationRequestVersion?> GetByIdForUpdateAsync(
        Guid customizationRequestVersionId,
        CancellationToken cancellationToken = default)
    {
        return DbSet
            .FirstOrDefaultAsync(
                version => version.CustomizationRequestVersionId == customizationRequestVersionId,
                cancellationToken);
    }

    public Task<CustomizationRequestVersion?> GetByIdWithRequestAsync(
        Guid customizationRequestVersionId,
        CancellationToken cancellationToken = default)
    {
        return DbSet
            .AsNoTracking()
            .Include(version => version.CustomizationRequest)
            .Include(version => version.ProductVersion)
            .FirstOrDefaultAsync(
                version => version.CustomizationRequestVersionId == customizationRequestVersionId,
                cancellationToken);
    }

    public async Task<int> GetNextVersionNoAsync(
        Guid customizationRequestId,
        CancellationToken cancellationToken = default)
    {
        var maxVersionNo = await DbSet
            .Where(version => version.CustomizationRequestId == customizationRequestId)
            .MaxAsync(version => (int?)version.VersionNo, cancellationToken);

        return (maxVersionNo ?? 0) + 1;
    }

    public async Task<IReadOnlyList<CustomizationRequestVersionReadModel>> GetByRequestIdAsync(
        Guid customizationRequestId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(version => version.CustomizationRequestId == customizationRequestId)
            .Join(
                DbContext.ProductVersionSet,
                version => version.ProductVersionId,
                productVersion => productVersion.ProductVersionId,
                (version, productVersion) => new { version, productVersion })
            .OrderBy(joined => joined.version.VersionNo)
            .Select(joined => new CustomizationRequestVersionReadModel
            {
                CustomizationRequestVersionId = joined.version.CustomizationRequestVersionId,
                CustomizationRequestId = joined.version.CustomizationRequestId,
                ProductVersionId = joined.version.ProductVersionId,
                VersionNo = joined.version.VersionNo,
                CreatedByDesignerId = joined.version.CreatedByDesignerId,
                VersionTitle = joined.version.VersionTitle,
                DesignerNote = joined.version.DesignerNote,
                Status = joined.version.Status,
                ProductionReviewedBy = joined.version.ProductionReviewedBy,
                FeasibilityStatus = joined.version.FeasibilityStatus,
                FeasibilityNote = joined.version.FeasibilityNote,
                EstimatedProductionDays = joined.version.EstimatedProductionDays,
                EstimatedAdditionalCost = joined.version.EstimatedAdditionalCost,
                AdditionalCostReason = joined.version.AdditionalCostReason,
                MaterialAvailable = joined.version.MaterialAvailable,
                ProductionRiskNote = joined.version.ProductionRiskNote,
                AlternativeMaterialNote = joined.version.AlternativeMaterialNote,
                SubmittedForReviewAt = joined.version.SubmittedForReviewAt,
                ProductionReviewedAt = joined.version.ProductionReviewedAt,
                ProductionRejectedAt = joined.version.ProductionRejectedAt,
                AcceptedAt = joined.version.AcceptedAt,
                WithdrawnAt = joined.version.WithdrawnAt,
                CreatedAt = joined.version.CreatedAt,
                UpdatedAt = joined.version.UpdatedAt,
                ProductVersion = joined.productVersion
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductionCustomizationVersionQueueReadModel>> GetProductionQueueAsync(
        ProductionCustomizationVersionQueueQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await ApplyProductionQueueFilters(BuildProductionQueueQuery(), query)
            .OrderByDescending(item => item.Version.SubmittedForReviewAt ?? item.Version.UpdatedAt)
            .ThenByDescending(item => item.Version.CustomizationRequestVersionId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountProductionQueueAsync(
        ProductionCustomizationVersionQueueQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return ApplyProductionQueueFilters(BuildProductionQueueQuery(), query)
            .CountAsync(cancellationToken);
    }

    public async Task<ProductionCustomizationVersionDetailReadModel?> GetProductionDetailAsync(
        Guid customizationRequestVersionId,
        CancellationToken cancellationToken = default)
    {
        var item = await BuildProductionQueueQuery()
            .Where(queueItem => queueItem.Version.CustomizationRequestVersionId == customizationRequestVersionId)
            .FirstOrDefaultAsync(cancellationToken);

        return item is null
            ? null
            : new ProductionCustomizationVersionDetailReadModel
            {
                Version = item.Version,
                Request = item.Request,
                ProposalName = item.ProposalName,
                ProposalStatus = item.ProposalStatus,
                SourceProductVersion = item.SourceProductVersion
            };
    }

    public async Task<bool> TryMarkProductionReviewedAsync(
        Guid customizationRequestVersionId,
        ProductionFeasibilityStatus feasibilityStatus,
        CustomizationVersionStatus versionStatus,
        Guid productionReviewedBy,
        string? feasibilityNote,
        int? estimatedProductionDays,
        decimal? estimatedAdditionalCost,
        string? additionalCostReason,
        bool? materialAvailable,
        string? productionRiskNote,
        string? alternativeMaterialNote,
        DateTime reviewedAt,
        CancellationToken cancellationToken = default)
    {
        var productionRejectedAt = feasibilityStatus == ProductionFeasibilityStatus.NOT_FEASIBLE
            ? reviewedAt
            : (DateTime?)null;

        var rowsAffected = await DbSet
            .Where(version =>
                version.CustomizationRequestVersionId == customizationRequestVersionId &&
                version.Status == CustomizationVersionStatus.REVIEWING &&
                version.FeasibilityStatus == ProductionFeasibilityStatus.PENDING)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(version => version.Status, versionStatus)
                    .SetProperty(version => version.FeasibilityStatus, feasibilityStatus)
                    .SetProperty(version => version.FeasibilityNote, feasibilityNote)
                    .SetProperty(version => version.EstimatedProductionDays, estimatedProductionDays)
                    .SetProperty(version => version.EstimatedAdditionalCost, estimatedAdditionalCost)
                    .SetProperty(version => version.AdditionalCostReason, additionalCostReason)
                    .SetProperty(version => version.MaterialAvailable, materialAvailable)
                    .SetProperty(version => version.ProductionRiskNote, productionRiskNote)
                    .SetProperty(version => version.AlternativeMaterialNote, alternativeMaterialNote)
                    .SetProperty(version => version.ProductionReviewedBy, productionReviewedBy)
                    .SetProperty(version => version.ProductionReviewedAt, reviewedAt)
                    .SetProperty(version => version.ProductionRejectedAt, productionRejectedAt)
                    .SetProperty(version => version.UpdatedAt, reviewedAt),
                cancellationToken);

        return rowsAffected > 0;
    }

    private IQueryable<ProductionCustomizationVersionQueueReadModel> BuildProductionQueueQuery()
    {
        return DbSet
            .AsNoTracking()
            .Join(
                DbContext.ProductVersionSet,
                version => version.ProductVersionId,
                productVersion => productVersion.ProductVersionId,
                (version, productVersion) => new { version, productVersion })
            .Join(
                DbContext.CustomizationRequestSet,
                joined => joined.version.CustomizationRequestId,
                request => request.CustomizationRequestId,
                (joined, request) => new { joined.version, joined.productVersion, request })
            .Join(
                DbContext.ProjectSet,
                joined => joined.request.ProjectId,
                project => project.ProjectId,
                (joined, project) => new { joined.version, joined.productVersion, joined.request, project })
            .Join(
                DbContext.ProposalSet,
                joined => joined.request.ProposalId,
                proposal => proposal.ProposalId,
                (joined, proposal) => new { joined.version, joined.productVersion, joined.request, joined.project, proposal })
            .Join(
                DbContext.ProductVersionSet,
                joined => joined.request.SourceProductVersionId,
                sourceVersion => sourceVersion.ProductVersionId,
                (joined, sourceVersion) => new ProductionCustomizationVersionQueueReadModel
                {
                    Version = new CustomizationRequestVersionReadModel
                    {
                        CustomizationRequestVersionId = joined.version.CustomizationRequestVersionId,
                        CustomizationRequestId = joined.version.CustomizationRequestId,
                        ProductVersionId = joined.version.ProductVersionId,
                        VersionNo = joined.version.VersionNo,
                        CreatedByDesignerId = joined.version.CreatedByDesignerId,
                        VersionTitle = joined.version.VersionTitle,
                        DesignerNote = joined.version.DesignerNote,
                        Status = joined.version.Status,
                        ProductionReviewedBy = joined.version.ProductionReviewedBy,
                        FeasibilityStatus = joined.version.FeasibilityStatus,
                        FeasibilityNote = joined.version.FeasibilityNote,
                        EstimatedProductionDays = joined.version.EstimatedProductionDays,
                        EstimatedAdditionalCost = joined.version.EstimatedAdditionalCost,
                        AdditionalCostReason = joined.version.AdditionalCostReason,
                        MaterialAvailable = joined.version.MaterialAvailable,
                        ProductionRiskNote = joined.version.ProductionRiskNote,
                        AlternativeMaterialNote = joined.version.AlternativeMaterialNote,
                        SubmittedForReviewAt = joined.version.SubmittedForReviewAt,
                        ProductionReviewedAt = joined.version.ProductionReviewedAt,
                        ProductionRejectedAt = joined.version.ProductionRejectedAt,
                        AcceptedAt = joined.version.AcceptedAt,
                        WithdrawnAt = joined.version.WithdrawnAt,
                        CreatedAt = joined.version.CreatedAt,
                        UpdatedAt = joined.version.UpdatedAt,
                        ProductVersion = joined.productVersion
                    },
                    Request = new CustomizationRequestReadModel
                    {
                        CustomizationRequestId = joined.request.CustomizationRequestId,
                        ProjectId = joined.request.ProjectId,
                        ProposalId = joined.request.ProposalId,
                        SourceProductVersionId = joined.request.SourceProductVersionId,
                        RequestedByCustomerId = joined.request.RequestedByCustomerId,
                        RequestTitle = joined.request.RequestTitle,
                        RequestDescription = joined.request.RequestDescription,
                        RequestedWidth = joined.request.RequestedWidth,
                        RequestedHeight = joined.request.RequestedHeight,
                        RequestedDepth = joined.request.RequestedDepth,
                        RequestedMaterial = joined.request.RequestedMaterial,
                        RequestedColor = joined.request.RequestedColor,
                        RequestedChangeNote = joined.request.RequestedChangeNote,
                        AcceptedRequestVersionId = joined.request.AcceptedRequestVersionId,
                        Status = joined.request.Status,
                        CreatedAt = joined.request.CreatedAt,
                        UpdatedAt = joined.request.UpdatedAt,
                        CustomerId = joined.project.CustomerId,
                        ProjectName = joined.project.ProjectName,
                        AssignedSalesId = joined.project.AssignedSalesId,
                        AssignedDesignerId = joined.project.AssignedDesignerId
                    },
                    ProposalName = joined.proposal.ProposalName,
                    ProposalStatus = joined.proposal.Status,
                    SourceProductVersion = sourceVersion
                });
    }

    private static IQueryable<ProductionCustomizationVersionQueueReadModel> ApplyProductionQueueFilters(
        IQueryable<ProductionCustomizationVersionQueueReadModel> query,
        ProductionCustomizationVersionQueueQueryReadModel filter)
    {
        if (filter.Statuses is { Count: > 0 })
        {
            query = query.Where(item => filter.Statuses.Contains(item.Version.Status));
        }

        if (filter.FeasibilityStatuses is { Count: > 0 })
        {
            query = query.Where(item => filter.FeasibilityStatuses.Contains(item.Version.FeasibilityStatus));
        }

        if (filter.ProjectId.HasValue)
        {
            query = query.Where(item => item.Request.ProjectId == filter.ProjectId.Value);
        }

        if (filter.ProposalId.HasValue)
        {
            query = query.Where(item => item.Request.ProposalId == filter.ProposalId.Value);
        }

        if (filter.MaterialAvailable.HasValue)
        {
            query = query.Where(item => item.Version.MaterialAvailable == filter.MaterialAvailable.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(
                item =>
                    item.Version.SubmittedForReviewAt.HasValue &&
                    item.Version.SubmittedForReviewAt.Value >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(
                item =>
                    item.Version.SubmittedForReviewAt.HasValue &&
                    item.Version.SubmittedForReviewAt.Value <= filter.ToDate.Value);
        }

        return query;
    }
}
