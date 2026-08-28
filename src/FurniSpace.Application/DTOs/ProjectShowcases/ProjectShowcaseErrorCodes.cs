namespace FurniSpace.Application.DTOs.ProjectShowcases;

public static class ProjectShowcaseErrorCodes
{
    public const string NotFound = "PROJECT_SHOWCASE_NOT_FOUND";
    public const string AlreadyExists = "PROJECT_SHOWCASE_ALREADY_EXISTS";
    public const string SlugDuplicate = "PROJECT_SHOWCASE_SLUG_DUPLICATE";
    public const string InvalidStatusTransition = "PROJECT_SHOWCASE_INVALID_STATUS_TRANSITION";
    public const string ProjectNotCompleted = "PROJECT_SHOWCASE_PROJECT_NOT_COMPLETED";
    public const string PublishRequirementsNotMet = "PROJECT_SHOWCASE_PUBLISH_REQUIREMENTS_NOT_MET";
    public const string MediaNotFound = "PROJECT_SHOWCASE_MEDIA_NOT_FOUND";
    public const string FileNotAllowed = "PROJECT_SHOWCASE_FILE_NOT_ALLOWED";
    public const string FileNotInProject = "PROJECT_SHOWCASE_FILE_NOT_IN_PROJECT";
    public const string FeaturedReviewInvalid = "PROJECT_SHOWCASE_FEATURED_REVIEW_INVALID";
    public const string ArchivedReadOnly = "PROJECT_SHOWCASE_ARCHIVED_READ_ONLY";
    public const string CoverConflict = "PROJECT_SHOWCASE_COVER_CONFLICT";
}
