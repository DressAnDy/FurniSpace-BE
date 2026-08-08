#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.Interfaces.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Admin;

public sealed class AdminCatalogController : BaseApiController
{
    private readonly IAdminCatalogService _adminCatalog;

    public AdminCatalogController(IAdminCatalogService adminCatalog)
    {
        _adminCatalog = adminCatalog;
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("/admin/catalog/products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] AdminCatalogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminCatalog.GetProductsAsync(query, cancellationToken);
        return ToActionResult(result);
    }
}
