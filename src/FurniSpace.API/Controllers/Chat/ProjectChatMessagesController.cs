#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Chat;

[Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
[Route("project-chats/{chatId:guid}/messages")]
public sealed class ProjectChatMessagesController : BaseApiController
{
    private readonly IProjectChatMessageService _messages;

    public ProjectChatMessagesController(IProjectChatMessageService messages)
    {
        _messages = messages;
    }

    [HttpPost]
    public async Task<IActionResult> SendTextMessage(
        Guid chatId,
        [FromBody] SendTextChatMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _messages.SendTextMessageAsync(
            chatId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("files/upload-url")]
    public async Task<IActionResult> PrepareFileMessageUpload(
        Guid chatId,
        [FromBody] PrepareProjectChatFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _messages.PrepareFileMessageUploadAsync(
            chatId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("files/complete")]
    public async Task<IActionResult> CompleteFileMessageUpload(
        Guid chatId,
        [FromBody] CompleteProjectChatFileUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _messages.CompleteFileMessageUploadAsync(
            chatId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetMessages(
        Guid chatId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 30,
        [FromQuery] string sort = "ASC",
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _messages.GetMessagesAsync(
            chatId,
            currentUserId,
            new ProjectChatMessageQueryDto
            {
                Page = page,
                Limit = limit,
                Sort = sort
            },
            cancellationToken);

        return ToActionResult(result);
    }
}
