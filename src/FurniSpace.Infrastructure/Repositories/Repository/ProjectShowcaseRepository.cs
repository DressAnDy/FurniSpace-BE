using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.ProjectShowcases;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectShowcaseRepository : IProjectShowcaseRepository
{
    private readonly AppDbContext _dbContext;

    public ProjectShowcaseRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(ProjectShowcase showcase, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseSet.AddAsync(showcase, cancellationToken).AsTask();
    }

    public Task<ProjectShowcase?> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseSet
            .AsNoTracking()
            .FirstOrDefaultAsync(showcase => showcase.ProjectId == projectId, cancellationToken);
    }

    public Task<ProjectShowcase?> GetByIdAsync(Guid showcaseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseSet
            .AsNoTracking()
            .FirstOrDefaultAsync(showcase => showcase.ProjectShowcaseId == showcaseId, cancellationToken);
    }

    public Task<ProjectShowcase?> GetForUpdateAsync(Guid showcaseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseSet
            .FirstOrDefaultAsync(showcase => showcase.ProjectShowcaseId == showcaseId, cancellationToken);
    }

    public async Task<ProjectShowcaseDetailReadModel?> GetDetailAsync(
        Guid showcaseId,
        CancellationToken cancellationToken = default)
    {
        var showcase = await (
            from item in _dbContext.ProjectShowcaseSet.AsNoTracking()
            join project in _dbContext.ProjectSet.AsNoTracking() on item.ProjectId equals project.ProjectId
            join review in _dbContext.ProjectReviewSet.AsNoTracking()
                on item.FeaturedReviewId equals review.ReviewId into reviews
            from review in reviews.DefaultIfEmpty()
            where item.ProjectShowcaseId == showcaseId
            select new ProjectShowcaseDetailReadModel
            {
                ProjectShowcaseId = item.ProjectShowcaseId,
                ProjectId = item.ProjectId,
                FeaturedReviewId = item.FeaturedReviewId,
                Title = item.Title,
                Slug = item.Slug,
                Summary = item.Summary,
                Description = item.Description,
                Status = item.Status,
                CreatedBy = item.CreatedBy,
                ApprovedBy = item.ApprovedBy,
                PublishedBy = item.PublishedBy,
                ApprovedAt = item.ApprovedAt,
                PublishedAt = item.PublishedAt,
                ArchivedAt = item.ArchivedAt,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                CustomerId = project.CustomerId,
                AssignedSalesId = project.AssignedSalesId,
                AssignedDesignerId = project.AssignedDesignerId,
                ProjectStatus = project.Status,
                ProjectName = project.ProjectName,
                BusinessType = project.BusinessType,
                FeaturedReviewAllowPublicDisplay = review != null && review.AllowPublicDisplay
            }).FirstOrDefaultAsync(cancellationToken);

        if (showcase is null)
        {
            return null;
        }

        var media = await GetMediaReadModelsAsync(showcase.ProjectShowcaseId, cancellationToken);
        return showcase with { Media = media };
    }

    public Task<bool> ProjectHasShowcaseAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseSet
            .AsNoTracking()
            .AnyAsync(showcase => showcase.ProjectId == projectId, cancellationToken);
    }

    public Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludeShowcaseId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ProjectShowcaseSet
            .AsNoTracking()
            .Where(showcase => showcase.Slug == slug);

        if (excludeShowcaseId.HasValue)
        {
            var showcaseId = excludeShowcaseId.Value;
            query = query.Where(showcase => showcase.ProjectShowcaseId != showcaseId);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task AddMediaAsync(ProjectShowcaseMedia media, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseMediaSet.AddAsync(media, cancellationToken).AsTask();
    }

    public Task<List<ProjectShowcaseMedia>> GetMediaForUpdateAsync(
        Guid showcaseId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseMediaSet
            .Where(media => media.ProjectShowcaseId == showcaseId)
            .OrderBy(media => media.DisplayOrder)
            .ThenBy(media => media.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<ProjectShowcaseMedia?> GetMediaForUpdateAsync(
        Guid showcaseId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseMediaSet
            .FirstOrDefaultAsync(
                media => media.ProjectShowcaseId == showcaseId && media.ProjectShowcaseMediaId == mediaId,
                cancellationToken);
    }

    public Task RemoveMediaAsync(ProjectShowcaseMedia media, CancellationToken cancellationToken = default)
    {
        _dbContext.ProjectShowcaseMediaSet.Remove(media);
        return Task.CompletedTask;
    }

    public async Task<int> GetNextMediaDisplayOrderAsync(Guid showcaseId, CancellationToken cancellationToken = default)
    {
        var maxOrder = await _dbContext.ProjectShowcaseMediaSet
            .Where(media => media.ProjectShowcaseId == showcaseId)
            .Select(media => (int?)media.DisplayOrder)
            .MaxAsync(cancellationToken);

        return (maxOrder ?? -1) + 1;
    }

    public Task<bool> HasCoverMediaAsync(Guid showcaseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseMediaSet
            .AsNoTracking()
            .AnyAsync(media => media.ProjectShowcaseId == showcaseId && media.IsCover, cancellationToken);
    }

    public Task<int> CountMediaAsync(Guid showcaseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseMediaSet
            .AsNoTracking()
            .CountAsync(media => media.ProjectShowcaseId == showcaseId, cancellationToken);
    }

    public Task<bool> HasInactiveMediaAsync(Guid showcaseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectShowcaseMediaSet
            .AsNoTracking()
            .AnyAsync(
                media => media.ProjectShowcaseId == showcaseId &&
                    !_dbContext.StoredFileSet.Any(file => file.FileId == media.FileId && file.Status == FileStatus.ACTIVE),
                cancellationToken);
    }

    public async Task<List<PublicShowcaseListItemReadModel>> GetPublishedPagedAsync(
        ProjectShowcaseListQueryReadModel query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var skip = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);
        var take = Math.Max(pageSize, 1);

        return await ApplyPublicSort(BuildPublishedListQuery(query), query.Sort)
            .Skip(skip)
            .Take(take)
            .Select(item => new PublicShowcaseListItemReadModel
            {
                ProjectShowcaseId = item.ProjectShowcaseId,
                ProjectName = item.ProjectName,
                Title = item.Title,
                Slug = item.Slug,
                Summary = item.Summary,
                Description = item.Description,
                BusinessType = item.BusinessType,
                PublishedAt = item.PublishedAt,
                CompletedAt = item.CompletedAt,
                TotalAreaSqm = item.TotalAreaSqm,
                CoverUrl = item.CoverUrl
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountPublishedAsync(
        ProjectShowcaseListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildPublishedListQuery(query).CountAsync(cancellationToken);
    }

    public async Task<PublicShowcaseDetailReadModel?> GetPublishedBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var detail = await (
            from showcase in _dbContext.ProjectShowcaseSet.AsNoTracking()
            join project in _dbContext.ProjectSet.AsNoTracking() on showcase.ProjectId equals project.ProjectId
            where showcase.Slug == slug && showcase.Status == ProjectShowcaseStatus.PUBLISHED
            select new PublicShowcaseDetailReadModel
            {
                ProjectShowcaseId = showcase.ProjectShowcaseId,
                Title = showcase.Title,
                Slug = showcase.Slug,
                Summary = showcase.Summary,
                Description = showcase.Description,
                ProjectName = project.ProjectName,
                BusinessType = project.BusinessType,
                PublishedAt = showcase.PublishedAt,
                CompletedAt = project.CompletedAt,
                SubmittedAt = project.SubmittedAt,
                TotalAreaSqm = project.TotalAreaSqm,
                NumberOfFloors = project.NumberOfFloors,
                ProjectAddress = project.ProjectAddress
            }).FirstOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return null;
        }

        var media = await GetMediaReadModelsAsync(detail.ProjectShowcaseId, cancellationToken);

        var review = await (
            from showcase in _dbContext.ProjectShowcaseSet.AsNoTracking()
            join featuredReview in _dbContext.ProjectReviewSet.AsNoTracking()
                on showcase.FeaturedReviewId equals featuredReview.ReviewId
            where showcase.ProjectShowcaseId == detail.ProjectShowcaseId
                && featuredReview.AllowPublicDisplay
            select new PublicShowcaseReviewReadModel
            {
                ReviewId = featuredReview.ReviewId,
                Rating = featuredReview.Rating,
                DesignQualityRating = featuredReview.DesignQualityRating,
                ServiceQualityRating = featuredReview.ServiceQualityRating,
                DeliveryRating = featuredReview.DeliveryRating,
                Comment = featuredReview.Comment
            }).FirstOrDefaultAsync(cancellationToken);

        return detail with
        {
            Media = media,
            Review = review
        };
    }

    public async Task<List<AdminProjectShowcaseListItemReadModel>> GetAdminPagedAsync(
        ProjectShowcaseListQueryReadModel query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var skip = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);
        var take = Math.Max(pageSize, 1);

        return await ApplyAdminSort(BuildAdminListQuery(query), query.Sort)
            .Skip(skip)
            .Take(take)
            .Select(item => new AdminProjectShowcaseListItemReadModel
            {
                ProjectShowcaseId = item.ProjectShowcaseId,
                ProjectId = item.ProjectId,
                ProjectName = item.ProjectName,
                BusinessType = item.BusinessType,
                Title = item.Title,
                Slug = item.Slug,
                Description = item.Description,
                Status = item.Status,
                CoverUrl = item.CoverUrl,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                PublishedAt = item.PublishedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAdminAsync(
        ProjectShowcaseListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildAdminListQuery(query).CountAsync(cancellationToken);
    }

    private IQueryable<ProjectShowcaseListProjection> BuildPublishedListQuery(ProjectShowcaseListQueryReadModel query)
    {
        return ApplyListFilters(BuildListProjection().Where(item => item.Status == ProjectShowcaseStatus.PUBLISHED), query);
    }

    private IQueryable<ProjectShowcaseListProjection> BuildAdminListQuery(ProjectShowcaseListQueryReadModel query)
    {
        var source = BuildListProjection();
        return ApplyListFilters(query.Status.HasValue ? source.Where(item => item.Status == query.Status.Value) : source, query);
    }

    private IQueryable<ProjectShowcaseListProjection> BuildListProjection()
    {
        return
            from showcase in _dbContext.ProjectShowcaseSet.AsNoTracking()
            join project in _dbContext.ProjectSet.AsNoTracking() on showcase.ProjectId equals project.ProjectId
            select new ProjectShowcaseListProjection
            {
                ProjectShowcaseId = showcase.ProjectShowcaseId,
                ProjectId = showcase.ProjectId,
                ProjectName = project.ProjectName,
                Title = showcase.Title,
                Slug = showcase.Slug,
                Summary = showcase.Summary,
                Description = showcase.Description,
                Status = showcase.Status,
                BusinessType = project.BusinessType,
                PublishedAt = showcase.PublishedAt,
                CompletedAt = project.CompletedAt,
                TotalAreaSqm = project.TotalAreaSqm,
                CreatedAt = showcase.CreatedAt,
                UpdatedAt = showcase.UpdatedAt,
                CoverUrl = (
                    from media in _dbContext.ProjectShowcaseMediaSet.AsNoTracking()
                    join file in _dbContext.StoredFileSet.AsNoTracking() on media.FileId equals file.FileId
                    where media.ProjectShowcaseId == showcase.ProjectShowcaseId && media.IsCover
                    select file.FileUrl).FirstOrDefault()
            };
    }

    private static IQueryable<ProjectShowcaseListProjection> ApplyListFilters(
        IQueryable<ProjectShowcaseListProjection> source,
        ProjectShowcaseListQueryReadModel query)
    {
        var filtered = source;
        if (!string.IsNullOrWhiteSpace(query.BusinessType))
        {
            var businessType = query.BusinessType.Trim().ToLower();
            filtered = filtered.Where(item => item.BusinessType != null && item.BusinessType.ToLower() == businessType);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            filtered = filtered.Where(item =>
                item.Title.ToLower().Contains(search) ||
                item.ProjectName.ToLower().Contains(search) ||
                item.Slug.ToLower().Contains(search) ||
                (item.BusinessType != null && item.BusinessType.ToLower().Contains(search)));
        }

        return filtered;
    }

    private static IQueryable<ProjectShowcaseListProjection> ApplyPublicSort(
        IQueryable<ProjectShowcaseListProjection> source,
        string? sort)
    {
        return NormalizeSort(sort) switch
        {
            "completeddate_asc" or "completed_at_asc" => source.OrderBy(item => item.CompletedAt).ThenBy(item => item.ProjectName),
            "area_asc" => source.OrderBy(item => item.TotalAreaSqm).ThenByDescending(item => item.CompletedAt),
            "area_desc" => source.OrderByDescending(item => item.TotalAreaSqm).ThenByDescending(item => item.CompletedAt),
            _ => source.OrderByDescending(item => item.CompletedAt).ThenBy(item => item.ProjectName)
        };
    }

    private static IQueryable<ProjectShowcaseListProjection> ApplyAdminSort(
        IQueryable<ProjectShowcaseListProjection> source,
        string? sort)
    {
        return NormalizeSort(sort) switch
        {
            "updatedat_asc" or "updated_at_asc" => source.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Title),
            "publishedat_desc" or "published_at_desc" => source.OrderByDescending(item => item.PublishedAt).ThenBy(item => item.Title),
            "publishedat_asc" or "published_at_asc" => source.OrderBy(item => item.PublishedAt).ThenBy(item => item.Title),
            _ => source.OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Title)
        };
    }

    private static string NormalizeSort(string? sort)
    {
        return string.IsNullOrWhiteSpace(sort) ? string.Empty : sort.Trim().ToLowerInvariant();
    }

    private async Task<IReadOnlyList<ProjectShowcaseMediaReadModel>> GetMediaReadModelsAsync(
        Guid showcaseId,
        CancellationToken cancellationToken)
    {
        return await (
            from media in _dbContext.ProjectShowcaseMediaSet.AsNoTracking()
            join file in _dbContext.StoredFileSet.AsNoTracking() on media.FileId equals file.FileId
            where media.ProjectShowcaseId == showcaseId && file.Status == FileStatus.ACTIVE
            orderby media.DisplayOrder, media.CreatedAt
            select new ProjectShowcaseMediaReadModel
            {
                ProjectShowcaseMediaId = media.ProjectShowcaseMediaId,
                FileId = media.FileId,
                MediaType = media.MediaType,
                Title = media.Title,
                Caption = media.Caption,
                IsCover = media.IsCover,
                DisplayOrder = media.DisplayOrder,
                FileUrl = file.FileUrl,
                OriginalFileName = file.OriginalFileName,
                MimeType = file.MimeType
            }).ToListAsync(cancellationToken);
    }

    private sealed class ProjectShowcaseListProjection
    {
        public Guid ProjectShowcaseId { get; init; }
        public Guid ProjectId { get; init; }
        public string ProjectName { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string? Summary { get; init; }
        public string? Description { get; init; }
        public ProjectShowcaseStatus Status { get; init; }
        public string? BusinessType { get; init; }
        public DateTime? PublishedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public decimal? TotalAreaSqm { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public string? CoverUrl { get; init; }
    }
}
