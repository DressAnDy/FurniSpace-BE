#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectReviews;
using FurniSpace.Application.Interfaces.ProjectReviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectReviewPublicConsentControllerTests
{
    [Fact]
    public void Update_RequiresCustomerRole()
    {
        var authorize = typeof(ProjectReviewPublicConsentController)
            .GetMethod(nameof(ProjectReviewPublicConsentController.Update))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("CUSTOMER", authorize.Roles);
    }

    [Fact]
    public async Task Update_ReturnsServiceResult()
    {
        var reviewId = Guid.NewGuid();
        var response = new ProjectReviewPublicConsentDto { ReviewId = reviewId, AllowPublicDisplay = true };
        var service = new FakeProjectReviewConsentService(
            ServiceResult<ProjectReviewPublicConsentDto>.Success(response));
        var controller = WithCustomer(new ProjectReviewPublicConsentController(service));

        var actionResult = await controller.Update(
            reviewId,
            new UpdateProjectReviewPublicConsentRequestDto { AllowPublicDisplay = true });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(reviewId, service.ReviewId);
        Assert.True(service.Request!.AllowPublicDisplay);
    }

    [Fact]
    public async Task Update_WithoutUser_ReturnsUnauthorized()
    {
        var controller = new ProjectReviewPublicConsentController(new FakeProjectReviewConsentService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var actionResult = await controller.Update(
            Guid.NewGuid(),
            new UpdateProjectReviewPublicConsentRequestDto { AllowPublicDisplay = false });

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static ProjectReviewPublicConsentController WithCustomer(
        ProjectReviewPublicConsentController controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "CUSTOMER")
                ],
                authenticationType: "Test"))
            }
        };
        return controller;
    }

    private sealed class FakeProjectReviewConsentService : IProjectReviewConsentService
    {
        private readonly ServiceResult<ProjectReviewPublicConsentDto>? _result;

        public FakeProjectReviewConsentService(
            ServiceResult<ProjectReviewPublicConsentDto>? result = null)
        {
            _result = result;
        }

        public Guid ReviewId { get; private set; }

        public UpdateProjectReviewPublicConsentRequestDto? Request { get; private set; }

        public Task<ServiceResult<ProjectReviewPublicConsentDto>> UpdatePublicConsentAsync(
            Guid reviewId,
            Guid currentUserId,
            UpdateProjectReviewPublicConsentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ReviewId = reviewId;
            Request = request;
            return Task.FromResult(_result ?? ServiceResult<ProjectReviewPublicConsentDto>.Unauthorized());
        }
    }
}
