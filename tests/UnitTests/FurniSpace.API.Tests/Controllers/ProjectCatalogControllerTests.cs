#nullable enable

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.Interfaces.Catalog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectCatalogControllerTests
{
    [Fact]
    public async Task GetProducts_WithoutUserContext_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeProjectCatalogService());

        var actionResult = await controller.GetProducts(Guid.NewGuid(), new ProjectCatalogQueryDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task GetProducts_WithDesignerContext_ReturnsServiceResult()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var response = new ProjectCatalogListResponseDto
        {
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };
        var service = new FakeProjectCatalogService(
            ServiceResult<ProjectCatalogListResponseDto>.Success(response, string.Empty));
        var controller = CreateController(service, userId, "DESIGNER");

        var actionResult = await controller.GetProducts(projectId, new ProjectCatalogQueryDto
        {
            Page = 1,
            PageSize = 20
        });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, service.LastProjectId);
        Assert.Equal(userId, service.LastUserId);
    }

    private static ProjectCatalogController CreateController(
        IProjectCatalogService service,
        Guid? userId = null,
        string? role = null)
    {
        var controller = new ProjectCatalogController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (userId.HasValue)
        {
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                new Claim(ClaimTypes.Role, role ?? "DESIGNER")
            ],
            "TestAuth"));
        }

        return controller;
    }

    private sealed class FakeProjectCatalogService : IProjectCatalogService
    {
        private readonly ServiceResult<ProjectCatalogListResponseDto> _listResult;

        public FakeProjectCatalogService(
            ServiceResult<ProjectCatalogListResponseDto>? listResult = null)
        {
            _listResult = listResult ??
                ServiceResult<ProjectCatalogListResponseDto>.Success(
                    new ProjectCatalogListResponseDto(),
                    string.Empty);
        }

        public Guid LastProjectId { get; private set; }
        public Guid LastUserId { get; private set; }

        public Task<ServiceResult<ProjectCatalogListResponseDto>> GetProductsAsync(
            Guid projectId,
            Guid currentUserId,
            string? role,
            ProjectCatalogQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            LastUserId = currentUserId;
            return Task.FromResult(_listResult);
        }

        public Task<ServiceResult<ProjectCatalogProductDetailDto>> GetProductByIdAsync(
            Guid projectId,
            Guid productId,
            Guid currentUserId,
            string? role,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectCatalogProductDetailDto>.Success(
                new ProjectCatalogProductDetailDto(),
                string.Empty));

        public Task<ServiceResult<ProjectCatalogProductVersionDetailDto>> GetProductVersionByIdAsync(
            Guid projectId,
            Guid productVersionId,
            Guid currentUserId,
            string? role,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectCatalogProductVersionDetailDto>.Success(
                new ProjectCatalogProductVersionDetailDto(),
                string.Empty));
    }
}
