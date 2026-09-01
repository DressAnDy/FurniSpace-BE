using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectShowcases;

public sealed class CreateProjectShowcaseRequestDto
{
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? Introduction { get; set; }
}

public sealed class UpdateProjectShowcaseRequestDto
{
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? Introduction { get; set; }
    public string? Slug { get; set; }
    public Guid? FeaturedReviewId { get; set; }
}

public sealed class AdminProjectShowcaseQueryDto
{
    public string? Search { get; set; }
    public ProjectShowcaseStatus? Status { get; set; }
    public string? BusinessType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Sort { get; set; }
}

public sealed class AddProjectShowcaseMediaRequestDto
{
    public Guid FileId { get; set; }
    public ProjectShowcaseMediaType MediaType { get; set; }
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public bool SetAsCover { get; set; }
}

public sealed class UploadProjectShowcaseMediaRequestDto
{
    public Stream Content { get; init; } = Stream.Null;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long FileSizeBytes { get; init; }
    public ProjectShowcaseMediaType MediaType { get; init; } = ProjectShowcaseMediaType.FINAL;
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public bool SetAsCover { get; set; }
}

public sealed class ReorderProjectShowcaseMediaRequestDto
{
    public List<Guid> MediaIds { get; set; } = [];
}

public sealed class ProjectShowcaseMediaDto
{
    public Guid ProjectShowcaseMediaId { get; set; }
    public Guid FileId { get; set; }
    public ProjectShowcaseMediaType MediaType { get; set; }
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public bool IsCover { get; set; }
    public int DisplayOrder { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
}

public sealed class ProjectShowcaseDto
{
    public Guid ProjectShowcaseId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? FeaturedReviewId { get; set; }
    public bool FeaturedReviewAllowPublicDisplay { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? Introduction { get; set; }
    public ProjectShowcaseStatus Status { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? BusinessType { get; set; }
    public ProjectStatus? ProjectStatus { get; set; }
    public string? CoverUrl { get; set; }
    public IReadOnlyList<ProjectShowcaseMediaDto> Media { get; set; } = [];
}

public sealed class AdminProjectShowcaseListItemDto
{
    public Guid ProjectShowcaseId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? BusinessType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Introduction { get; set; }
    public ProjectShowcaseStatus Status { get; set; }
    public string? CoverUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public sealed class AdminProjectShowcaseListResponseDto
{
    public List<AdminProjectShowcaseListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public sealed class PublicShowcaseQueryDto
{
    public string? Search { get; set; }
    public string? BusinessType { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public sealed class PublicShowcaseListItemDto
{
    public Guid ProjectShowcaseId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Introduction { get; set; }
    public string? CoverUrl { get; set; }
    public string? BusinessType { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public decimal? TotalAreaSqm { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public sealed class PublicShowcaseListResponseDto
{
    public List<PublicShowcaseListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public sealed class PublicShowcaseReviewDto
{
    public Guid ReviewId { get; set; }
    public int? Rating { get; set; }
    public int? DesignQualityRating { get; set; }
    public int? ServiceQualityRating { get; set; }
    public int? DeliveryRating { get; set; }
    public string? Comment { get; set; }
}

public sealed class PublicShowcaseDetailDto
{
    public Guid ProjectShowcaseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? Introduction { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? BusinessType { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public decimal? TotalAreaSqm { get; set; }
    public int? NumberOfFloors { get; set; }
    public int? ImplementationDurationDays { get; set; }
    public string? ProjectAddress { get; set; }
    public int? CompletionYear { get; set; }
    public string? CoverUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public PublicShowcaseReviewDto? Review { get; set; }
    public IReadOnlyList<ProjectShowcaseMediaDto> Media { get; set; } = [];
}
