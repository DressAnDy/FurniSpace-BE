using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProjectShowcases;

public sealed record ProjectShowcaseDetailReadModel
{
    public Guid ProjectShowcaseId { get; init; }
    public Guid ProjectId { get; init; }
    public Guid? FeaturedReviewId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public ProjectShowcaseStatus Status { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? ApprovedBy { get; init; }
    public Guid? PublishedBy { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? ArchivedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? AssignedSalesId { get; init; }
    public Guid? AssignedDesignerId { get; init; }
    public ProjectStatus? ProjectStatus { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string? BusinessType { get; init; }
    public bool FeaturedReviewAllowPublicDisplay { get; init; }
    public IReadOnlyList<ProjectShowcaseMediaReadModel> Media { get; init; } = [];
}

public sealed class ProjectShowcaseMediaReadModel
{
    public Guid ProjectShowcaseMediaId { get; init; }
    public Guid FileId { get; init; }
    public ProjectShowcaseMediaType MediaType { get; init; }
    public string? Title { get; init; }
    public string? Caption { get; init; }
    public bool IsCover { get; init; }
    public int DisplayOrder { get; init; }
    public string FileUrl { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
}

public sealed class PublicShowcaseListItemReadModel
{
    public Guid ProjectShowcaseId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? CoverUrl { get; init; }
    public string? BusinessType { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public decimal? TotalAreaSqm { get; init; }
}

public sealed record ProjectShowcaseListQueryReadModel(
    string? Search,
    ProjectShowcaseStatus? Status,
    string? BusinessType,
    string? Sort);

public sealed class AdminProjectShowcaseListItemReadModel
{
    public Guid ProjectShowcaseId { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string? BusinessType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ProjectShowcaseStatus Status { get; init; }
    public string? CoverUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
}

public sealed record PublicShowcaseDetailReadModel
{
    public Guid ProjectShowcaseId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string? BusinessType { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public decimal? TotalAreaSqm { get; init; }
    public int? NumberOfFloors { get; init; }
    public string? ProjectAddress { get; init; }
    public PublicShowcaseReviewReadModel? Review { get; init; }
    public IReadOnlyList<ProjectShowcaseMediaReadModel> Media { get; init; } = [];
}

public sealed class PublicShowcaseReviewReadModel
{
    public Guid ReviewId { get; init; }
    public int? Rating { get; init; }
    public int? DesignQualityRating { get; init; }
    public int? ServiceQualityRating { get; init; }
    public int? DeliveryRating { get; init; }
    public string? Comment { get; init; }
}
