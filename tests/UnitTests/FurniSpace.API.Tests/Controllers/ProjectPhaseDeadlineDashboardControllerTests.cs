#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Dashboard;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Dashboard;
using FurniSpace.Application.Interfaces.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectPhaseDeadlineDashboardControllerTests
{
    [Fact]
    public void GetProjectPhaseDeadlines_RequiresDashboardRoles()
    {
        var authorize = typeof(ProjectPhaseDeadlineDashboardController)
            .GetMethod(nameof(ProjectPhaseDeadlineDashboardController.GetProjectPhaseDeadlines))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("SALES,DESIGNER,PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetProjectPhaseDeadlines_ForwardsQueryToService()
    {
        var userId = Guid.NewGuid();
        var service = new FakeDashboardQueueService();
        var controller = WithUser(new ProjectPhaseDeadlineDashboardController(service), userId);
        var query = new ProjectPhaseDeadlineRiskQueryDto { Phase = "PROPOSAL", Status = "OVERDUE" };

        var actionResult = await controller.GetProjectPhaseDeadlines(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var result = Assert.IsType<ServiceResult<ProjectPhaseDeadlineRiskResponseDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Same(query, service.Query);
    }

    [Fact]
    public async Task GetProjectPhaseDeadlines_WhenUserIdMissing_ReturnsUnauthorized()
    {
        var controller = new ProjectPhaseDeadlineDashboardController(new FakeDashboardQueueService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.GetProjectPhaseDeadlines(new ProjectPhaseDeadlineRiskQueryDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static ProjectPhaseDeadlineDashboardController WithUser(
        ProjectPhaseDeadlineDashboardController controller,
        Guid userId)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString("D"))],
                    "Test"))
            }
        };
        return controller;
    }

    private sealed class FakeDashboardQueueService : IDashboardQueueService
    {
        public Guid CurrentUserId { get; private set; }

        public ProjectPhaseDeadlineRiskQueryDto? Query { get; private set; }

        public Task<ServiceResult<ProjectPhaseDeadlineRiskResponseDto>> GetProjectPhaseDeadlineRisksAsync(
            Guid currentUserId,
            ProjectPhaseDeadlineRiskQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            Query = query;
            return Task.FromResult(ServiceResult<ProjectPhaseDeadlineRiskResponseDto>.Success(new ProjectPhaseDeadlineRiskResponseDto()));
        }

        public Task<ServiceResult<DashboardQueueResponseDto>> GetSalesActionQueueAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ServiceResult<SalesDashboardKpisDto>> GetSalesKpisAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ServiceResult<DashboardQueueResponseDto>> GetDesignerWorkQueueAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ServiceResult<DesignerDashboardKpisDto>> GetDesignerKpisAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ServiceResult<DashboardQueueResponseDto>> GetProductionQueueAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ServiceResult<ProductionDashboardKpisDto>> GetProductionKpisAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
