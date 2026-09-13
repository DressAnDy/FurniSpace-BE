using FurniSpace.Domain.Entities;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectReviewRepository
{
    Task<ProjectReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default);

    Task<ProjectReview?> GetForUpdateAsync(Guid reviewId, CancellationToken cancellationToken = default);

    Task<ProjectReview?> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task AddAsync(ProjectReview review, CancellationToken cancellationToken = default);
}
