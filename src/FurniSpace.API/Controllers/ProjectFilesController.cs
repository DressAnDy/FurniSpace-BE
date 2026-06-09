#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
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
    private readonly IProjectFileService _projectFiles;

    public ProjectFilesController(IProjectFileService projectFiles)
    {
        _projectFiles = projectFiles;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(104_857_600)]
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
            new UploadProjectFileRequestDto
            {
                Content = request.File?.OpenReadStream() ?? Stream.Null,
                OriginalFileName = request.File?.FileName ?? string.Empty,
                ContentType = request.File?.ContentType ?? "application/octet-stream",
                FileSizeBytes = request.File?.Length ?? 0,
                FileType = request.FileType,
                Visibility = request.Visibility,
                Note = request.Note
            },
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

public sealed class UploadProjectFileFormRequest
{
    public IFormFile? File { get; set; }
    public FileType FileType { get; set; } = FileType.OTHER;
    public FileVisibility? Visibility { get; set; }
    public string? Note { get; set; }
}
