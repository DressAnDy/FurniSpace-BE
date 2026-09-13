#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.Interfaces.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
[Route("admin/projects")]
public sealed class AdminProjectsController : BaseApiController
{
    private readonly IProjectWorkflowService _workflows;

    public AdminProjectsController(IProjectWorkflowService workflows)
    {
        _workflows = workflows;
    }

    [HttpGet("{projectId:guid}/workflow")]
    public async Task<IActionResult> GetWorkflow(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var result = await _workflows.GetWorkflowAsync(projectId, cancellationToken);
        return ToActionResult(result);
    }
}
