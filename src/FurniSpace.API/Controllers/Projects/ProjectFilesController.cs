#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Interfaces.ProjectFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("projects/{projectId:guid}/files")]
public sealed class ProjectFilesController : BaseApiController
{
    private readonly IProjectFileService _projectFiles;

    public ProjectFilesController(IProjectFileService projectFiles)
    {
        _projectFiles = projectFiles;
    }

    [HttpPost("upload-url")]
    public async Task<IActionResult> PrepareProjectFileUpload(
        Guid projectId,
        [FromBody] PrepareProjectFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.PrepareProjectFileUploadAsync(
            projectId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("complete")]
    public async Task<IActionResult> CompleteProjectFileUpload(
        Guid projectId,
        [FromBody] CompleteProjectFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.CompleteProjectFileUploadAsync(
            projectId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetProjectFiles(
        Guid projectId,
        [FromQuery] ProjectFilesQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.GetProjectFilesAsync(
            projectId,
            currentUserId,
            query,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchProjectFiles(
        Guid projectId,
        [FromQuery] string query,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.SearchProjectFilesAsync(
            projectId,
            currentUserId,
            query,
            page,
            limit,
            cancellationToken);

        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
