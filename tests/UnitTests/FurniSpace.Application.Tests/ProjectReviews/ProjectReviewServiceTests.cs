#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.ProjectReviews;
using FurniSpace.Application.Services.ProjectReviews;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectReviews;

public sealed class ProjectReviewServiceTests
{
    [Fact]
    public async Task GetByProjectAsync_WhenReviewExists_ReturnsReview()
    {
        await using var context = CreateContext();
        var data = await SeedCompletedProjectAsync(context, withReview: true);
        var service = CreateService(context);

        var result = await service.GetByProjectAsync(data.ProjectId, data.CustomerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(data.ReviewId, result.Data!.ReviewId);
        Assert.Equal(5, result.Data.Rating);
    }

    [Fact]
    public async Task GetByProjectAsync_WhenReviewMissing_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var data = await SeedCompletedProjectAsync(context, withReview: false);
        var service = CreateService(context);

        var result = await service.GetByProjectAsync(data.ProjectId, data.CustomerId);

        Assert.Equal(404, result.Status);
        Assert.Equal(ProjectReviewErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetByProjectAsync_WhenNotProjectOwner_ReturnsForbidden()
    {
        await using var context = CreateContext();
        var data = await SeedCompletedProjectAsync(context, withReview: true);
        var service = CreateService(context);

        var result = await service.GetByProjectAsync(data.ProjectId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
        Assert.Equal(ProjectReviewErrorCodes.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_CreatesReview()
    {
        await using var context = CreateContext();
        var data = await SeedCompletedProjectAsync(context, withReview: false, withOrder: true);
        var service = CreateService(context);

        var result = await service.CreateAsync(
            data.ProjectId,
            data.CustomerId,
            new CreateProjectReviewRequestDto
            {
                Rating = 4,
                DesignQualityRating = 5,
                ServiceQualityRating = 4,
                DeliveryRating = 3,
                Comment = " Good service "
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(4, result.Data!.Rating);
        Assert.Equal("Good service", result.Data.Comment);
        Assert.Equal(data.OrderId, result.Data.OrderId);

        var review = await context.ProjectReviewSet.SingleAsync();
        Assert.Equal(data.ProjectId, review.ProjectId);
    }

    [Fact]
    public async Task CreateAsync_WhenProjectNotCompleted_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var data = await SeedCompletedProjectAsync(context, withReview: false, projectStatus: ProjectStatus.IN_PRODUCTION);
        var service = CreateService(context);

        var result = await service.CreateAsync(
            data.ProjectId,
            data.CustomerId,
            new CreateProjectReviewRequestDto
            {
                Rating = 4,
                DesignQualityRating = 4,
                ServiceQualityRating = 4,
                DeliveryRating = 4
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectReviewErrorCodes.ProjectNotCompleted, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenReviewAlreadyExists_ReturnsConflict()
    {
        await using var context = CreateContext();
        var data = await SeedCompletedProjectAsync(context, withReview: true);
        var service = CreateService(context);

        var result = await service.CreateAsync(
            data.ProjectId,
            data.CustomerId,
            new CreateProjectReviewRequestDto
            {
                Rating = 4,
                DesignQualityRating = 4,
                ServiceQualityRating = 4,
                DeliveryRating = 4
            });

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectReviewErrorCodes.AlreadyExists, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenRatingInvalid_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var data = await SeedCompletedProjectAsync(context, withReview: false);
        var service = CreateService(context);

        var result = await service.CreateAsync(
            data.ProjectId,
            data.CustomerId,
            new CreateProjectReviewRequestDto
            {
                Rating = 6,
                DesignQualityRating = 4,
                ServiceQualityRating = 4,
                DeliveryRating = 4
            });

        Assert.Equal(400, result.Status);
        Assert.Equal("PROJECT_REVIEW_RATING_INVALID", result.ErrorCode);
    }

    private static ProjectReviewService CreateService(AppDbContext context) =>
        new(
            new ProjectReviewRepository(context),
            new ProjectRepository(context),
            new OrderRepository(context),
            new UnitOfWork(context));

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedCompletedProjectAsync(
        AppDbContext context,
        bool withReview,
        bool withOrder = false,
        ProjectStatus projectStatus = ProjectStatus.COMPLETED)
    {
        var customerRoleId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        context.RoleSet.Add(new Role { RoleId = customerRoleId, RoleName = "CUSTOMER" });
        context.AccountSet.Add(new Account
        {
            AccountId = customerId,
            RoleId = customerRoleId,
            Email = "customer@example.com",
            FullName = "Customer",
            PasswordHash = "hash",
            Status = AccountStatus.ACTIVE
        });
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectName = "Cafe",
            Status = projectStatus
        });

        if (withOrder)
        {
            context.OrderSet.Add(new Order
            {
                OrderId = orderId,
                ProjectId = projectId,
                QuotationId = Guid.NewGuid(),
                OrderCode = "ORD-REV",
                CustomerId = customerId,
                Status = OrderStatus.COMPLETED,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (withReview)
        {
            context.ProjectReviewSet.Add(new ProjectReview
            {
                ReviewId = reviewId,
                ProjectId = projectId,
                CustomerId = customerId,
                Rating = 5,
                DesignQualityRating = 5,
                ServiceQualityRating = 5,
                DeliveryRating = 5,
                Comment = "Great",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
        return new SeededData(projectId, customerId, reviewId, withOrder ? orderId : null);
    }

    private sealed record SeededData(Guid ProjectId, Guid CustomerId, Guid ReviewId, Guid? OrderId);
}
