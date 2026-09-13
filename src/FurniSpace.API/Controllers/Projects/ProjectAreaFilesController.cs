#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
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
    private readonly IProjectFileService _projectFiles;

    public ProjectAreaFilesController(IProjectFileService projectFiles)
    {
        _projectFiles = projectFiles;
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPost("upload-url")]
    public async Task<IActionResult> PrepareProjectAreaFileUpload(
        Guid projectAreaId,
        [FromBody] PrepareProjectFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.PrepareProjectAreaFileUploadAsync(
            projectAreaId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteProjectAreaFileUpload(
        Guid projectAreaId,
        [FromBody] CompleteProjectFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.CompleteProjectAreaFileUploadAsync(
            projectAreaId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet]
    public async Task<IActionResult> GetProjectAreaFiles(
        Guid projectAreaId,
        [FromQuery] ProjectFilesQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectFiles.GetProjectAreaFilesAsync(
            projectAreaId,
            currentUserId,
            query,
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPatch("{fileId:guid}/primary")]
    public async Task<IActionResult> SetPrimaryFile(
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
