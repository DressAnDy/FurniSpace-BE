#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

[Authorize]
[Route("projects/{projectId:guid}/chats")]
public sealed class ProjectChatsController : BaseApiController
{
    private readonly IProjectChatService _projectChats;

    public ProjectChatsController(IProjectChatService projectChats)
    {
        _projectChats = projectChats;
    }

    [Authorize(Roles = "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [HttpGet]
    public async Task<IActionResult> GetList(
        Guid projectId,
        [FromQuery] ProjectChatStatus? status = null,
        [FromQuery] ProjectChatType? chatType = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectChats.GetProjectChatsAsync(
            projectId,
            currentUserId,
            new ProjectChatListQueryDto
            {
                Status = status,
                ChatType = chatType,
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
