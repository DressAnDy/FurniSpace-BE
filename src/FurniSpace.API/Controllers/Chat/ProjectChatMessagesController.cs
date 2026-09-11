#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Chat;

[Authorize(Roles = "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
[Route("project-chats/{chatId:guid}/messages")]
public sealed class ProjectChatMessagesController : BaseApiController
{
    private const long MultipartRequestLimitBytes = 100L * 1024L * 1024L;

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

    [HttpPost("files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MultipartRequestLimitBytes)]
    public async Task<IActionResult> SendFileMessage(
        Guid chatId,
        [FromForm] SendFileChatMessageFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _messages.SendFileMessageAsync(
            chatId,
            currentUserId,
            new SendFileChatMessageRequestDto
            {
                FileContent = request.File?.OpenReadStream() ?? Stream.Null,
                OriginalFileName = request.File?.FileName ?? string.Empty,
                ContentType = request.File?.ContentType ?? "application/octet-stream",
                FileSizeBytes = request.File?.Length ?? 0,
                FileType = request.FileType,
                Visibility = request.Visibility,
                Content = request.Content
            },
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

public sealed class SendFileChatMessageFormRequest
{
    public IFormFile? File { get; set; }
    public string? Content { get; set; }
    public FileType FileType { get; set; } = FileType.OTHER;
    public FileVisibility? Visibility { get; set; }
}
