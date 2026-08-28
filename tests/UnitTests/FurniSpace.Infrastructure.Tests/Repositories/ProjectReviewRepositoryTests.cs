#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class ProjectReviewRepositoryTests
{
    [Fact]
    public async Task GetByProjectIdAsync_ReturnsReview()
    {
        await using var context = CreateContext();
        var reviewId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        context.ProjectReviewSet.Add(new ProjectReview
        {
            ReviewId = reviewId,
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            Rating = 4,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new ProjectReviewRepository(context);

        var review = await repository.GetByProjectIdAsync(projectId);

        Assert.NotNull(review);
        Assert.Equal(reviewId, review!.ReviewId);
    }

    [Fact]
    public async Task ExistsByProjectIdAsync_ReturnsTrueWhenReviewExists()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        context.ProjectReviewSet.Add(new ProjectReview
        {
            ReviewId = Guid.NewGuid(),
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            Rating = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new ProjectReviewRepository(context);

        var exists = await repository.ExistsByProjectIdAsync(projectId);

        Assert.True(exists);
    }

    [Fact]
    public async Task AddAsync_PersistsReview()
    {
        await using var context = CreateContext();
        var repository = new ProjectReviewRepository(context);
        var review = new ProjectReview
        {
            ReviewId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Rating = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(review);
        await context.SaveChangesAsync();

        Assert.Equal(1, await context.ProjectReviewSet.CountAsync());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
