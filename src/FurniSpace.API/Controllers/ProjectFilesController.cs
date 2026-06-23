#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.API.DTOs.ProjectFiles;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Interfaces.ProjectFiles;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

[Authorize]
[Route("projects/{projectId:guid}/files")]
public sealed class ProjectFilesController : BaseApiController
{
    private const long MultipartRequestLimitBytes = 100L * 1024L * 1024L;

    private readonly IProjectFileService _projectFiles;

    public ProjectFilesController(IProjectFileService projectFiles)
    {
        _projectFiles = projectFiles;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    // Allows multipart overhead while ProjectFileService enforces configured file-size limits.
    [RequestSizeLimit(MultipartRequestLimitBytes)]
    public async Task<IActionResult> UploadProjectFile(
        Guid projectId,
        [FromForm] UploadProjectFileFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.UploadProjectFileAsync(
            projectId,
            currentUserId,
            request.ToRequestDto(),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetProjectFiles(
        Guid projectId,
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

        var result = await _projectFiles.GetProjectFilesAsync(
            projectId,
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

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
