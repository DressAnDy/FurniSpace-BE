#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.Interfaces.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize(Roles = "DESIGNER,ADMIN")]
[Route("projects/{projectId:guid}/catalog")]
public sealed class ProjectCatalogController : BaseApiController
{
    private readonly IProjectCatalogService _projectCatalog;

    public ProjectCatalogController(IProjectCatalogService projectCatalog)
    {
        _projectCatalog = projectCatalog;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        Guid projectId,
        [FromQuery] ProjectCatalogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserContext(out var currentUserId, out var role))
        {
            return Unauthorized();
        }

        var result = await _projectCatalog.GetProductsAsync(
            projectId,
            currentUserId,
            role,
            query,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("products/{productId:guid}")]
    public async Task<IActionResult> GetProductById(
        Guid projectId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserContext(out var currentUserId, out var role))
        {
            return Unauthorized();
        }

        var result = await _projectCatalog.GetProductByIdAsync(
            projectId,
            productId,
            currentUserId,
            role,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("product-versions/{productVersionId:guid}")]
    public async Task<IActionResult> GetProductVersionById(
        Guid projectId,
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserContext(out var currentUserId, out var role))
        {
            return Unauthorized();
        }

        var result = await _projectCatalog.GetProductVersionByIdAsync(
            projectId,
            productVersionId,
            currentUserId,
            role,
            cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserContext(out Guid currentUserId, out string? role)
    {
        currentUserId = Guid.Empty;
        role = User.FindFirstValue(ClaimTypes.Role);
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
