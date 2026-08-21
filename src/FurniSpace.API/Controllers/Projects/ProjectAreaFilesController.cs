#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.API.DTOs.ProjectFiles;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Interfaces.ProjectFiles;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("project-areas/{projectAreaId:guid}/files")]
public sealed class ProjectAreaFilesController : BaseApiController
{
    private const long MultipartRequestLimitBytes = 100L * 1024L * 1024L;

    private readonly IProjectFileService _projectFiles;

    public ProjectAreaFilesController(IProjectFileService projectFiles)
    {
        _projectFiles = projectFiles;
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MultipartRequestLimitBytes)]
    public async Task<IActionResult> UploadProjectAreaFile(
        Guid projectAreaId,
        [FromForm] UploadProjectFileFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.UploadProjectAreaFileAsync(
            projectAreaId,
            currentUserId,
            request.ToRequestDto(),
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet]
    public async Task<IActionResult> GetProjectAreaFiles(
        Guid projectAreaId,
        [FromQuery] FileType? fileType = null,
        [FromQuery] FileVisibility? visibility = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.GetProjectAreaFilesAsync(
            projectAreaId,
            currentUserId,
            new ProjectFilesQueryDto
            {
                FileType = fileType,
                Visibility = visibility,
                Page = page,
                Limit = limit
            },
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPatch("{fileId:guid}/primary")]
    public async Task<IActionResult> SetPrimary(
        Guid projectAreaId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.SetProjectAreaPrimaryFileAsync(
            projectAreaId,
            fileId,
            currentUserId,
            cancellationToken);

        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
