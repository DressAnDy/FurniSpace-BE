#nullable enable

using FurniSpace.Application.Common;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.ProjectReviews;
using FurniSpace.Application.Interfaces.ProjectReviews;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Services.ProjectReviews;

public sealed class ProjectReviewService : IProjectReviewService
{
    private const int MinRating = 1;
    private const int MaxRating = 5;

    private readonly IProjectReviewRepository _reviews;
    private readonly IProjectRepository _projects;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectReviewService(
        IProjectReviewRepository reviews,
        IProjectRepository projects,
        IOrderRepository orders,
        IUnitOfWork unitOfWork)
    {
        _reviews = reviews;
        _projects = projects;
        _orders = orders;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<ProjectReviewDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateCustomerProjectAccessAsync(projectId, currentUserId, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var review = await _reviews.GetByProjectIdAsync(projectId, cancellationToken);
        if (review is null)
        {
            return ServiceResult<ProjectReviewDto>.Failure(
                Error.NotFound(ProjectReviewErrorCodes.NotFound, "Project review was not found."));
        }

        return ServiceResult<ProjectReviewDto>.Success(ToDto(review), "Project review retrieved successfully.");
    }

    public async Task<ServiceResult<ProjectReviewDto>> CreateAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProjectReviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var accessError = await ValidateCustomerProjectAccessAsync(projectId, currentUserId, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectReviewDto>.Failure(
                Error.NotFound("PROJECT_NOT_FOUND", "Project was not found."));
        }

        if (project.Status != ProjectStatus.COMPLETED)
        {
            return ServiceResult<ProjectReviewDto>.Failure(
                Error.BadRequest(ProjectReviewErrorCodes.ProjectNotCompleted, "Project must be completed before submitting a review."));
        }

        if (await _reviews.ExistsByProjectIdAsync(projectId, cancellationToken))
        {
            return ServiceResult<ProjectReviewDto>.Failure(
                Error.Conflict(ProjectReviewErrorCodes.AlreadyExists, "A review already exists for this project."));
        }

        if (!IsValidRating(request.Rating)
            || !IsValidRating(request.DesignQualityRating)
            || !IsValidRating(request.ServiceQualityRating)
            || !IsValidRating(request.DeliveryRating))
        {
            return ServiceResult<ProjectReviewDto>.Failure(
                Error.BadRequest("PROJECT_REVIEW_RATING_INVALID", "Review ratings must be between 1 and 5."));
        }

        var relatedOrders = await _orders.GetByProjectAsync(projectId, cancellationToken);
        var orderId = relatedOrders
            .Where(order => order.Status != OrderStatus.CANCELLED)
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => (Guid?)order.OrderId)
            .FirstOrDefault();

        var now = DateTime.UtcNow;
        var review = new ProjectReview
        {
            ReviewId = Guid.NewGuid(),
            ProjectId = projectId,
            OrderId = orderId,
            CustomerId = currentUserId,
            Rating = request.Rating,
            DesignQualityRating = request.DesignQualityRating,
            ServiceQualityRating = request.ServiceQualityRating,
            DeliveryRating = request.DeliveryRating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            AllowPublicDisplay = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _reviews.AddAsync(review, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProjectReviewDto>.Success(ToDto(review), "Project review created successfully.");
    }

    private async Task<ServiceResult<ProjectReviewDto>?> ValidateCustomerProjectAccessAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return ServiceResult<ProjectReviewDto>.BadRequest("Project id and current user are required.");
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectReviewDto>.NotFound("PROJECT_NOT_FOUND");
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsCustomer(roleName) || project.CustomerId != currentUserId)
        {
            return ServiceResult<ProjectReviewDto>.Failure(
                Error.Forbidden(ProjectReviewErrorCodes.Forbidden, "Only the project owner can access this review."));
        }

        return null;
    }

    private static bool IsCustomer(string? roleName) =>
        string.Equals(roleName, ApplicationRoles.Customer, StringComparison.OrdinalIgnoreCase);

    private static bool IsValidRating(int rating) => rating is >= MinRating and <= MaxRating;

    private static ProjectReviewDto ToDto(ProjectReview review) =>
        new()
        {
            ReviewId = review.ReviewId,
            ProjectId = review.ProjectId,
            OrderId = review.OrderId,
            CustomerId = review.CustomerId,
            Rating = review.Rating,
            DesignQualityRating = review.DesignQualityRating,
            ServiceQualityRating = review.ServiceQualityRating,
            DeliveryRating = review.DeliveryRating,
            Comment = review.Comment,
            AllowPublicDisplay = review.AllowPublicDisplay,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };
}
