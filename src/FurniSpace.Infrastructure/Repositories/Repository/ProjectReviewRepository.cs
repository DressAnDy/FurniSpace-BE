using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectReviewRepository : IProjectReviewRepository
{
    private readonly AppDbContext _dbContext;

    public ProjectReviewRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProjectReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectReviewSet
            .AsNoTracking()
            .FirstOrDefaultAsync(review => review.ReviewId == reviewId, cancellationToken);
    }

    public Task<ProjectReview?> GetForUpdateAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectReviewSet
            .FirstOrDefaultAsync(review => review.ReviewId == reviewId, cancellationToken);
    }

    public Task<ProjectReview?> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectReviewSet
            .AsNoTracking()
            .FirstOrDefaultAsync(review => review.ProjectId == projectId, cancellationToken);
    }

    public Task<bool> ExistsByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectReviewSet
            .AsNoTracking()
            .AnyAsync(review => review.ProjectId == projectId, cancellationToken);
    }

    public Task AddAsync(ProjectReview review, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProjectReviewSet.AddAsync(review, cancellationToken).AsTask();
    }
}
