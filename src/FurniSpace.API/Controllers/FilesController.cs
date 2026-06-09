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
[Route("files")]
public sealed class FilesController : BaseApiController
{
    private readonly IProjectFileService _projectFiles;

    public FilesController(IProjectFileService projectFiles)
    {
        _projectFiles = projectFiles;
    }

    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> GetFileDetail(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.GetFileDetailAsync(fileId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("by-reference")]
    public async Task<IActionResult> GetFilesByReference(
        [FromQuery] string referenceType,
        [FromQuery] Guid referenceId,
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

        var result = await _projectFiles.GetFilesByReferenceAsync(
            currentUserId,
            new FilesByReferenceQueryDto
            {
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                FileType = fileType,
                Visibility = visibility,
                Page = page,
                Limit = limit
            },
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPatch("{fileId:guid}/archive")]
    public async Task<IActionResult> ArchiveFile(
        Guid fileId,
        [FromBody] ArchiveFileRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.ArchiveFileAsync(
            fileId,
            currentUserId,
            request ?? new ArchiveFileRequestDto(),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{fileId:guid}")]
    public async Task<IActionResult> DeleteFile(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.DeleteFileAsync(fileId, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
