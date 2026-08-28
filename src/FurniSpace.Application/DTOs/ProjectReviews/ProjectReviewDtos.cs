namespace FurniSpace.Application.DTOs.ProjectReviews;

public static class ProjectReviewErrorCodes
{
    public const string NotFound = "PROJECT_REVIEW_NOT_FOUND";
    public const string ConsentForbidden = "PROJECT_REVIEW_CONSENT_FORBIDDEN";
    public const string AlreadyExists = "PROJECT_REVIEW_ALREADY_EXISTS";
    public const string ProjectNotCompleted = "PROJECT_NOT_COMPLETED";
    public const string Forbidden = "PROJECT_REVIEW_FORBIDDEN";
}

public sealed class CreateProjectReviewRequestDto
{
    public int Rating { get; set; }
    public int DesignQualityRating { get; set; }
    public int ServiceQualityRating { get; set; }
    public int DeliveryRating { get; set; }
    public string? Comment { get; set; }
}

public sealed class ProjectReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public int? Rating { get; set; }
    public int? DesignQualityRating { get; set; }
    public int? ServiceQualityRating { get; set; }
    public int? DeliveryRating { get; set; }
    public string? Comment { get; set; }
    public bool AllowPublicDisplay { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class UpdateProjectReviewPublicConsentRequestDto
{
    public bool AllowPublicDisplay { get; set; }
}

public sealed class ProjectReviewPublicConsentDto
{
    public Guid ReviewId { get; set; }
    public Guid ProjectId { get; set; }
    public bool AllowPublicDisplay { get; set; }
    public DateTime? PublicDisplayConsentAt { get; set; }
}
