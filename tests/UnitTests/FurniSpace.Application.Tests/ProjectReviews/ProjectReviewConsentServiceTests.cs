#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.ProjectReviews;
using FurniSpace.Application.Services.ProjectReviews;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectReviews;

public sealed class ProjectReviewConsentServiceTests
{
    [Fact]
    public async Task UpdatePublicConsentAsync_CustomerAllowsPublicDisplay_SetsConsentTimestamp()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var service = CreateService(context);

        var result = await service.UpdatePublicConsentAsync(
            data.ReviewId,
            data.CustomerId,
            new UpdateProjectReviewPublicConsentRequestDto { AllowPublicDisplay = true });

        Assert.Equal(200, result.Status);
        Assert.True(result.Data!.AllowPublicDisplay);
        Assert.NotNull(result.Data.PublicDisplayConsentAt);

        var review = await context.ProjectReviewSet.SingleAsync(entity => entity.ReviewId == data.ReviewId);
        Assert.True(review.AllowPublicDisplay);
        Assert.NotNull(review.PublicDisplayConsentAt);
    }

    [Fact]
    public async Task UpdatePublicConsentAsync_WhenNotReviewOwner_ReturnsForbidden()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var service = CreateService(context);

        var result = await service.UpdatePublicConsentAsync(
            data.ReviewId,
            Guid.NewGuid(),
            new UpdateProjectReviewPublicConsentRequestDto { AllowPublicDisplay = true });

        Assert.Equal(403, result.Status);
    }

    private static ProjectReviewConsentService CreateService(AppDbContext context)
    {
        return new ProjectReviewConsentService(
            new ProjectReviewRepository(context),
            new ProjectRepository(context),
            new UnitOfWork(context));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(AppDbContext context)
    {
        var customerRoleId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();

        context.RoleSet.Add(new Role { RoleId = customerRoleId, RoleName = "CUSTOMER" });
        context.AccountSet.Add(new Account
        {
            AccountId = customerId,
            RoleId = customerRoleId,
            Email = "customer@example.com",
            FullName = "Customer",
            PasswordHash = "hash",
            Status = Domain.Enums.AccountStatus.ACTIVE
        });
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectName = "Cafe",
            Status = Domain.Enums.ProjectStatus.COMPLETED
        });
        context.ProjectReviewSet.Add(new ProjectReview
        {
            ReviewId = reviewId,
            ProjectId = projectId,
            CustomerId = customerId,
            Rating = 5,
            Comment = "Great work",
            AllowPublicDisplay = false
        });
        await context.SaveChangesAsync();
        return new SeededData(reviewId, customerId);
    }

    private sealed record SeededData(Guid ReviewId, Guid CustomerId);
}
