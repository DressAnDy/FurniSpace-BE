namespace FurniSpace.Application.DTOs.ProjectReviews;

public static class ProjectReviewErrorCodes
{
    public const string NotFound = "PROJECT_REVIEW_NOT_FOUND";
    public const string ConsentForbidden = "PROJECT_REVIEW_CONSENT_FORBIDDEN";
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
