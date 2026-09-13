#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Application.Interfaces.ProjectShowcases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Public;

[AllowAnonymous]
[Route("public/showcases")]
public sealed class PublicShowcasesController : BaseApiController
{
    private readonly IProjectShowcaseService _showcases;

    public PublicShowcasesController(IProjectShowcaseService showcases)
    {
        _showcases = showcases;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] PublicShowcaseQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _showcases.GetPublicListAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var result = await _showcases.GetPublicBySlugAsync(slug, cancellationToken);
        return ToActionResult(result);
    }
}
