#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.MeasurementImages;
using FurniSpace.Application.Interfaces.MeasurementImages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("project-areas/{projectAreaId:guid}/measurement-images")]
public sealed class ProjectAreaMeasurementImagesController : BaseApiController
{
    private readonly IMeasurementImageService _measurementImages;

    public ProjectAreaMeasurementImagesController(IMeasurementImageService measurementImages)
    {
        _measurementImages = measurementImages;
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet]
    public async Task<IActionResult> GetMeasurementImages(
        Guid projectAreaId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _measurementImages.GetProjectAreaMeasurementImagesAsync(
            projectAreaId,
            currentUserId,
            new MeasurementImageGalleryQueryDto
            {
                Page = page,
                Limit = limit
            },
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpPost("{fileId:guid}/link")]
    public async Task<IActionResult> LinkMeasurementImage(
        Guid projectAreaId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _measurementImages.LinkMeasurementImageToAreaAsync(
            projectAreaId,
            fileId,
            currentUserId,
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,DESIGNER,ADMIN")]
    [HttpDelete("{fileId:guid}/link")]
    public async Task<IActionResult> UnlinkMeasurementImage(
        Guid projectAreaId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _measurementImages.UnlinkMeasurementImageFromAreaAsync(
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
