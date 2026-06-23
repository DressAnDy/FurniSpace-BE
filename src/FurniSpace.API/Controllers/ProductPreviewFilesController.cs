#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

[Route("products")]
public sealed class ProductPreviewFilesController : BaseApiController
{
    private readonly IProductPreviewImageService _previewImages;

    public ProductPreviewFilesController(IProductPreviewImageService previewImages)
    {
        _previewImages = previewImages;
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("{productId:guid}/preview-files/reorder")]
    public async Task<IActionResult> Reorder(
        Guid productId,
        [FromBody] ReorderProductPreviewImagesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _previewImages.ReorderAsync(productId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpDelete("{productId:guid}/preview-files/{fileId:guid}")]
    public async Task<IActionResult> Delete(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var result = await _previewImages.DeleteAsync(productId, fileId, cancellationToken);
        return ToActionResult(result);
    }
}
