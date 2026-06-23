#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.Interfaces.ProductVersions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

[Route("ProductVersions")]
public sealed class ProductVersionPreviewFilesController : BaseApiController
{
    private readonly IProductVersionService _productVersions;

    public ProductVersionPreviewFilesController(IProductVersionService productVersions)
    {
        _productVersions = productVersions;
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("product-versions/{productVersionId:guid}/preview-files/reorder")]
    public async Task<IActionResult> Reorder(
        Guid productVersionId,
        [FromBody] ReorderProductVersionPreviewFilesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.ReorderPreviewFilesAsync(
            productVersionId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpDelete("product-versions/{productVersionId:guid}/preview-files/{fileId:guid}")]
    public async Task<IActionResult> Delete(
        Guid productVersionId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.DeletePreviewFileAsync(productVersionId, fileId, cancellationToken);
        return ToActionResult(result);
    }
}
