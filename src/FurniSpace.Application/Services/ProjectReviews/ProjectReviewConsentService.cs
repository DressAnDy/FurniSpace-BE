using FurniSpace.Application.Common;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.ProjectReviews;
using FurniSpace.Application.Interfaces.ProjectReviews;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Services.ProjectReviews;

public sealed class ProjectReviewConsentService : IProjectReviewConsentService
{
    private const string ConsentUpdatedMessage = "Project review public display consent updated successfully.";

    private readonly IProjectReviewRepository _reviews;
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectReviewConsentService(
        IProjectReviewRepository reviews,
        IProjectRepository projects,
        IUnitOfWork unitOfWork)
    {
        _reviews = reviews;
        _projects = projects;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<ProjectReviewPublicConsentDto>> UpdatePublicConsentAsync(
        Guid reviewId,
        Guid currentUserId,
        UpdateProjectReviewPublicConsentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (reviewId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectReviewPublicConsentDto>.BadRequest("Review id and current user are required.");
        }

        var review = await _reviews.GetForUpdateAsync(reviewId, cancellationToken);
        if (review is null)
        {
            return ServiceResult<ProjectReviewPublicConsentDto>.NotFound(ProjectReviewErrorCodes.NotFound);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsCustomer(roleName) || review.CustomerId != currentUserId)
        {
            return ServiceResult<ProjectReviewPublicConsentDto>.Forbidden(
                "Only the customer who submitted the review can update public display consent.");
        }

        var now = DateTime.UtcNow;
        review.AllowPublicDisplay = request.AllowPublicDisplay;
        review.PublicDisplayConsentAt = request.AllowPublicDisplay ? now : null;
        review.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectReviewPublicConsentDto>.Success(
            new ProjectReviewPublicConsentDto
            {
                ReviewId = review.ReviewId,
                ProjectId = review.ProjectId,
                AllowPublicDisplay = review.AllowPublicDisplay,
                PublicDisplayConsentAt = review.PublicDisplayConsentAt
            },
            ConsentUpdatedMessage);
    }

    private static bool IsCustomer(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Customer, StringComparison.OrdinalIgnoreCase);
    }
}
