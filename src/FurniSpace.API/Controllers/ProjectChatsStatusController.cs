#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

[Authorize(Roles = "SALES,DESIGNER,ADMIN")]
[Route("project-chats/{chatId:guid}/status")]
public sealed class ProjectChatsStatusController : BaseApiController
{
    private readonly IProjectChatService _projectChats;

    public ProjectChatsStatusController(IProjectChatService projectChats)
    {
        _projectChats = projectChats;
    }

    [HttpPatch]
    public async Task<IActionResult> UpdateStatus(
        Guid chatId,
        [FromBody] UpdateProjectChatStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _projectChats.UpdateStatusAsync(
            chatId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }
}
