#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.Interfaces.Reports;
using FurniSpace.Shared.DTOs.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
[Route("admin/reports")]
public sealed class AdminReportsController : BaseApiController
{
    private readonly IAdminReportService _reports;

    public AdminReportsController(IAdminReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetOverviewAsync(from, to, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("business")]
    public async Task<IActionResult> GetBusiness(CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetBusinessAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetProjectsAsync(from, to, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("projects/aging")]
    public async Task<IActionResult> GetProjectAging(
        [FromQuery] ProjectAgingQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetProjectAgingAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("commercial")]
    public async Task<IActionResult> GetCommercial(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetCommercialAsync(from, to, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("commercial/trend")]
    public async Task<IActionResult> GetCommercialTrend(
        [FromQuery] CommercialTrendQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetCommercialTrendAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("production")]
    public async Task<IActionResult> GetProduction(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetProductionAsync(from, to, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("delivery")]
    public async Task<IActionResult> GetDelivery(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetDeliveryAsync(from, to, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("delivery/reviews")]
    public async Task<IActionResult> GetDeliveryReviews(
        [FromQuery] DeliveryReviewsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetDeliveryReviewsAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetCatalogAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("catalog/bestsellers")]
    public async Task<IActionResult> GetCatalogBestsellers(
        [FromQuery] CatalogBestsellersQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.GetCatalogBestsellersAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] ReportExportQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _reports.ExportAsync(query, cancellationToken);
        if (result.Status != 200 || result.Data is null)
        {
            return ToActionResult(result);
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }
}
