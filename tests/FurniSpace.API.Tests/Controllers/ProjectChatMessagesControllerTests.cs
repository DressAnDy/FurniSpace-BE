#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectChatMessagesControllerTests
{
    [Fact]
    public void Controller_UsesExpectedRouteAndParticipantRoles()
    {
        var route = typeof(ProjectChatMessagesController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();
        var authorize = typeof(ProjectChatMessagesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();
        var httpGet = typeof(ProjectChatMessagesController)
            .GetMethod(nameof(ProjectChatMessagesController.GetMessages))!
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>()
            .Single();

        Assert.Equal("project-chats/{chatId:guid}/messages", route.Template);
        Assert.Equal("CUSTOMER,SALES,DESIGNER,ADMIN", authorize.Roles);
        Assert.Null(httpGet.Template);
    }

    [Fact]
    public void SendTextMessage_UsesPostEndpoint()
    {
        var httpPost = typeof(ProjectChatMessagesController)
            .GetMethod(nameof(ProjectChatMessagesController.SendTextMessage))!
            .GetCustomAttributes(typeof(HttpPostAttribute), inherit: false)
            .Cast<HttpPostAttribute>()
            .Single();

        Assert.Null(httpPost.Template);
    }

    [Fact]
    public async Task GetMessages_ReturnsServiceResultAndPassesQuery()
    {
        var chatId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var response = new ProjectChatMessageListResponseDto
        {
            Items = [new ProjectChatMessageDto { MessageId = Guid.NewGuid(), ChatId = chatId }],
            Page = 2,
            Limit = 15,
            Total = 1
        };
        var service = new FakeProjectChatMessageService(
            ServiceResult<ProjectChatMessageListResponseDto>.Success(
                response,
                "Chat messages retrieved successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.GetMessages(chatId, page: 2, limit: 15, sort: "DESC");

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectChatMessageListResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(chatId, service.ChatId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.NotNull(service.Query);
        Assert.Equal(2, service.Query.Page);
        Assert.Equal(15, service.Query.Limit);
        Assert.Equal("DESC", service.Query.Sort);
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task GetMessages_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectChatMessageService(
            ServiceResult<ProjectChatMessageListResponseDto>.Success(
                new ProjectChatMessageListResponseDto()));
        var controller = BuildController(service);

        var actionResult = await controller.GetMessages(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task SendTextMessage_ReturnsCreatedAndPassesAuthenticatedUser()
    {
        var chatId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new SendTextChatMessageRequestDto
        {
            Content = "Hello project team"
        };
        var response = new ProjectChatMessageDto
        {
            MessageId = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = currentUserId,
            MessageType = "TEXT",
            Content = request.Content
        };
        var service = new FakeProjectChatMessageService(
            ServiceResult<ProjectChatMessageDto>.Created(response, "Message sent successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.SendTextMessage(chatId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectChatMessageDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(chatId, service.ChatId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.SendRequest);
        Assert.Equal(1, service.SendCallCount);
    }

    [Fact]
    public async Task SendTextMessage_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectChatMessageService(
            ServiceResult<ProjectChatMessageDto>.Created(new ProjectChatMessageDto()));
        var controller = BuildController(service);

        var actionResult = await controller.SendTextMessage(
            Guid.NewGuid(),
            new SendTextChatMessageRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(0, service.SendCallCount);
    }

    private static ProjectChatMessagesController BuildController(
        IProjectChatMessageService service,
        Guid? currentUserId = null)
    {
        var claims = currentUserId.HasValue
            ? new[] { new Claim(ClaimTypes.NameIdentifier, currentUserId.Value.ToString()) }
            : [];

        return new ProjectChatMessagesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            }
        };
    }

    private sealed class FakeProjectChatMessageService : IProjectChatMessageService
    {
        private readonly ServiceResult<ProjectChatMessageListResponseDto>? _getResult;
        private readonly ServiceResult<ProjectChatMessageDto>? _sendResult;

        public FakeProjectChatMessageService(ServiceResult<ProjectChatMessageListResponseDto> result)
        {
            _getResult = result;
        }

        public FakeProjectChatMessageService(ServiceResult<ProjectChatMessageDto> result)
        {
            _sendResult = result;
        }

        public int CallCount { get; private set; }
        public int SendCallCount { get; private set; }
        public Guid ChatId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public ProjectChatMessageQueryDto? Query { get; private set; }
        public SendTextChatMessageRequestDto? SendRequest { get; private set; }

        public Task<bool> CanAccessChatAsync(
            Guid chatId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<ServiceResult<ProjectChatMessageListResponseDto>> GetMessagesAsync(
            Guid chatId,
            Guid currentUserId,
            ProjectChatMessageQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ChatId = chatId;
            CurrentUserId = currentUserId;
            Query = query;
            return Task.FromResult(_getResult!);
        }

        public Task<ServiceResult<ProjectChatMessageDto>> SendTextMessageAsync(
            Guid chatId,
            Guid currentUserId,
            SendTextChatMessageRequestDto request,
            CancellationToken cancellationToken = default)
        {
            SendCallCount++;
            ChatId = chatId;
            CurrentUserId = currentUserId;
            SendRequest = request;
            return Task.FromResult(_sendResult!);
        }
    }
}
